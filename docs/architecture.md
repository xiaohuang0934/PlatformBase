# 架构设计 / Architecture Design

## 分层架构 / Layered Architecture

PlatformBase 采用 Clean Architecture（DDD 分层），依赖方向单向内聚。

```
┌──────────────────────────────────────────────────┐
│  PlatformBase.Host                               │
│  ┌────────────────┐ ┌─────────────────────────┐ │
│  │ Controllers    │ │ Middleware Pipeline      │ │
│  │ AuthController │ │ GlobalException → CORS   │ │
│  │ HealthController│ │ → IdentityServer → Auth │ │
│  └───────┬────────┘ │ → Authorization → Swagger│ │
│          │          └─────────────────────────┘ │
│  ┌───────▼────────┐ ┌─────────────────────────┐ │
│  │ IdentityServer4 │ │ Service Implementations │ │
│  │ Token Issuance  │ │ UserService             │ │
│  │ JWT Signing     │ │ PermissionService       │ │
│  └────────────────┘ │ CurrentUserContext       │ │
│                      └────────┬────────────────┘ │
├───────────────────────────────┼──────────────────┤
│  PlatformBase.Infrastructure  │                  │
│  ┌────────────────────────────▼────────────────┐ │
│  │ AppDbContext (inherits DbContext)           │ │
│  │ ├ Audit auto-fill (*By)                    │ │
│  │ ├ Soft-delete global filter                │ │
│  │ └ Entity configurations                    │ │
│  ├ UnitOfWork + IRepository<T>                │ │
│  └ ServiceCollectionExtensions                │ │
├───────────────────────────────────────────────-─┤
│  PlatformBase.Application                       │
│  ┌────────────────────────────────────────────┐ │
│  │ DTOs + Service Interfaces                  │ │
│  │ IUserService / IPermissionService          │ │
│  └────────────────────────────────────────────┘ │
├──────────────────────────────────────────────────┤
│  PlatformBase.Core                              │
│  ┌────────────────────────────────────────────┐ │
│  │ Entity Hierarchy + Repository Contracts    │ │
│  │ ICurrentUserContext + ApiResult<T>         │ │
│  │ BusinessException + ErrorCode              │ │
│  └────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────┘
```

## 依赖方向 / Dependency Flow

```
Host → Infrastructure → Application → Core ← 无外部依赖
```

- **Core** zero external dependencies — pure C# classes
- **Application** depends only on Core
- **Infrastructure** depends on Application + EF Core NuGet
- **Host** depends on all layers + IdentityServer4 + Redis + Swashbuckle

## 中间件管道 / Middleware Pipeline

Pipeline order is **strict and must not be reordered**:

```
① GlobalExceptionMiddleware   — 最前端，捕获所有后续中间件的异常
② UseRouting                  — 路由匹配，必须在 IdentityServer 之前
③ UseCors                     — 跨域，必须在路由之后
④ UseIdentityServer           — 拦截 /connect/token & /.well-known/*
⑤ UseAuthentication            — JWT Bearer 验证 token 签名+有效期+issuer+audience
⑥ UseAuthorization             — [Permission("code")] 鉴权
⑦ Swagger UI                  — 仅开发环境
⑧ MapControllers              — API 端点
```

### 各中间件依赖关系

| 顺序 | 中间件 | 前置依赖 | 说明 |
|------|--------|---------|------|
| ④ | UseIdentityServer | 必须在 UseRouting 之后、UseAuthentication 之前 | IdentityServer 需要路由匹配后才拦截 /connect/token |
| ⑤ | UseAuthentication | IdentityServer 已签发 token | JwtBearer 验证签名（同一 X509 密钥） |
| ⑥ | UseAuthorization | HttpContext.User 已填充 | PermissionHandler 从 User 取 UserId 查询权限 |
| ⑦ | Swagger UI | 无依赖 | 仅开发环境，生产环境应关闭 |

## 签名密钥方案 / Signing Key Strategy

IdentityServer4 签发 JWT + JwtBearer 验证 JWT 共享一把 X509 自签名证书密钥：

```csharp
// 启动时生成 X509 自签名证书（纯托管代码，跨平台兼容）
using var rsa = RSA.Create(2048);
var certRequest = new CertificateRequest(
    "CN=PlatformBase", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
var cert = certRequest.CreateSelfSigned(
    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
var signingKey = new X509SecurityKey(cert);

// IdentityServer4 使用此密钥签发 JWT
.AddSigningCredential(new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256))

// JwtBearer 使用同一密钥验证 JWT（零 HTTP 发现调用）
IssuerSigningKey = signingKey
```

### 为什么是 X509 而非 RSA.Create()？

`RSA.Create(2048)` 在 macOS 上返回 Apple Security Framework 实现，该实现与 `RsaSecurityKey` 在 JWT 签名/验签时存在不兼容性。`CertificateRequest.CreateSelfSigned()` 是纯托管 .NET 代码，跨平台一致。

> 生产环境应替换为真实 X509 证书（从文件/Base64/证书存储加载），详见 [部署指南](deployment.md)。

## 实体继承体系 / Entity Hierarchy

```
IEntity<TKey>              ← 泛型主键接口
  └─ BaseEntity<TKey>      ← 泛型主键基类
       └─ BaseEntity(Guid) ← 默认 Guid 主键
            ├─ AuditableEntity  ← + CreatedAt / CreatedBy / UpdatedAt / UpdatedBy
            │    ├─ SoftDeleteEntity ← + IsDeleted / DeletedAt / DeletedBy
            │    │    └─ User          ← + PasswordHash / SecurityStamp / Lockout
            │    └─ Role              ← + Name / Description
            └─ Permission            ← + Code / ResourcePath / HttpMethod
```

### 审计自动填充 / Audit Auto-Fill

`AppDbContext.SaveChangesAsync` 在持久化前通过 `ICurrentUserContext` 自动填充：

| 字段 / Field | 触发时机 / Trigger | 值来源 / Source |
|-------------|-------------------|----------------|
| `CreatedAt` / `CreatedBy` | `EntityState.Added` | `DateTime.UtcNow` / `_currentUser.UserName` |
| `UpdatedAt` / `UpdatedBy` | `EntityState.Modified` | `DateTime.UtcNow` / `_currentUser.UserName` |
| `DeletedAt` / `DeletedBy` | `ISoftDelete.IsDeleted=true` | `DateTime.UtcNow` / `_currentUser.UserName` |

未登录时 `CreatedBy`/`UpdatedBy`/`DeletedBy` 填充 `"system"`。

### 软删除全局过滤器 / Soft-Delete Global Filter

```csharp
// 自动为所有 ISoftDelete 实体注册查询过滤器
modelBuilder.Entity<T>.HasQueryFilter(e => !e.IsDeleted);
// 所有 IRepository<T>.GetAllAsync / FindAsync / GetPagedAsync 自动排除已删除记录
```

## 统一响应模型 / Unified Response

```json
{
  "success": true,   // 请求是否成功
  "code": 200,       // 业务/HTTP 状态码
  "message": "success",
  "data": { ... },   // 泛型响应数据
  "traceId": null    // 链路追踪 ID
}
```

- `ApiResult<T>` — 带数据的泛型版本
- `ApiResult` — 不带数据的非泛型版本（用于无返回值的操作）
- `BusinessException` — 业务异常，由 `GlobalExceptionMiddleware` 自动捕获并转为 ApiResult.Fail

## 错误码体系 / Error Code System

| 范围 / Range | 说明 / Purpose |
|--------------|----------------|
| 4xx | HTTP 标准状态码（400/401/403/404/409/422） |
| 5xx | 服务器错误（500） |
| 1000-1999 | 业务错误（1001 重复记录 / 1002 数据不存在 / 1003 非法操作） |
| 1004-1010 | 认证权限错误（令牌过期/无效/用户不存在/密码错误/锁定/频控/权限拒绝） |
| 2000-2999 | 基础设施错误（2001 数据库错误） |
| 3000-3999 | 外部服务错误（3001 外部服务调用失败） |

## 仓储 + 工作单元 / Repository + Unit of Work

- `IRepository<T>` — where `T : BaseEntity`（Guid 主键），提供 CRUD + 分页 + 软删除
- `IKeyedRepository<T, TKey>` — where `T : BaseEntity<TKey>`（泛型主键）
- `IUnitOfWork` — 惰性创建 Repository 实例 + 事务支持（Begin/Commit/Rollback）

关联表（`UserRole`、`RolePermission`、`UserPermission`）使用复合主键，不继承 `BaseEntity`，通过 `AppDbContext.Set<T>()` 直接操作，不走 Repository。

## 日志 / Logging

Serilog 双输出：

- **Console** — 实时控制台输出，格式 `[yyyy-MM-dd HH:mm:ss Level] SourceContext: Message`
- **File** — 按天滚动写入 `logs/log-{date}.txt`，保留最近 30 天
- **Minimum Level** — Information（EF Core 为 Warning）

---

> **文档导航**：[docs/README.md](README.md) — 全部文档索引，含按角色推荐阅读路径
