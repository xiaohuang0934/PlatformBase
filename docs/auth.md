# 认证授权体系 / Authentication & Authorization

## 概述 / Overview

PlatformBase 采用 **自建 User 体系 + IdentityServer4 + JWT Bearer** 架构：

- **User 实体** — 继承 `SoftDeleteEntity`，密码使用 BCrypt 哈希
- **IdentityServer4** — OAuth2 Token 签发服务（Password Grant），嵌入式同进程部署
- **JwtBearer** — 验证请求中的 Bearer token（共享 X509 签名密钥）
- **ICurrentUserContext** — 注入到 Service / DbContext，提供当前用户会话上下文 + 租户范围

## User 实体 / User Entity

```
User : SoftDeleteEntity (继承审计 + 软删除)
  ├ Username / NormalizedUsername      — 登录名 + 大写索引
  ├ Email / NormalizedEmail            — 邮箱 + 大写索引
  ├ PasswordHash                       — BCrypt 哈希（含算法+盐+哈希）
  ├ SecurityStamp                      — 密码变更时更新，旧 token 失效
  ├ TenantId (nullable)                — 所属租户（null = 平台账号）
  ├ UserType                           — 用户类型枚举
  ├ LockoutEnd / LockoutEnabled        — 失败锁定
  ├ AccessFailedCount                  — 累计失败次数
  └ IsActive                           — 账号启用/禁用
```

## 用户类型枚举 / UserType Enum

| 值 | 类型 | 说明 |
|----|------|------|
| 1 | `PlatformAdmin` | 平台管理员（跨租户，TenantId=null） |
| 2 | `TenantAdmin` | 租户管理员（TenantId=所属租户） |
| 3 | `TenantUser` | 租户普通用户（TenantId=所属租户） |

## 登录流程 / Login Flow

```
POST /api/auth/login (AllowAnonymous)
  │
  ├─ ① IUserService.GetByUsername → 查 User
  │
  ├─ ② 状态检查：IsActive? LockoutEnd? 
  │
  ├─ ③ BCrypt.Verify(password, hash) → 验证
  │     ↓ 失败 → RecordLoginFailed (递增 AccessFailedCount)
  │         → ≥5 次失败 → LockoutEnd = Now+5min → 锁定
  │
  ├─ ④ RecordLoginSuccess → 清零 AccessFailedCount
  │
  ├─ ⑤ 构建 Claims（UserId + UserName + SecurityStamp + UserType）
  │
  └─ ⑥ IdentityServerTools.IssueJwtAsync(lifetime:3600, claims)
      → 返回 JWT access_token
```

## JWT 签发与验证 / JWT Issuance & Validation

**签发端（AuthController.Login）**：

```csharp
var claims = new List<Claim> {
    new(ClaimTypes.NameIdentifier, user.Id),
    new(ClaimTypes.Name, user.Username),
    new("aud", "api1"),
    new("security_stamp", user.SecurityStamp),
    new("user_type", userType.ToString())
};

var token = await _identityServerTools.IssueJwtAsync(3600, claims);
```

> JWT 仅包含身份核心信息。租户信息（TenantId/TenantIds）从数据库查询 + Redis 缓存获取，确保实时性。

**验证端（JwtBearer Middleware）**：

```csharp
.AddJwtBearer(options => {
    options.TokenValidationParameters = new() {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = signingKey,
        ValidateIssuer = true,
        ValidIssuer = "http://localhost:5269",
        ValidateAudience = true,
        ValidAudience = "api1",
        ValidateLifetime = true,
    };
});
```

**签名密钥**：IdentityServer 和 JwtBearer 共享同一个 X509 自签名证书（详见 [架构设计](architecture.md)）。

## ICurrentUserContext — 会话上下文注入 / Session Context Injection

```csharp
public interface ICurrentUserContext
{
    // ═══════ 身份信息（来自 JWT Claims）═══════
    Guid? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
    string? IpAddress { get; }
    string? ClientId { get; }
    UserType UserType { get; }
    
    // ═══════ 租户范围（DB 查询 + Redis 缓存）═══════
    Guid? TenantId { get; }              // 归属租户（租户用户有值，平台用户 null）
    Guid? CurrentTenantId { get; }       // 当前视角租户（用于单租户过滤）
    IReadOnlyList<Guid> TenantIds { get; } // 平台用户分配的租户列表
    IReadOnlyList<Guid> CurrentTenantIds { get; } // 当前生效的租户列表
    
    // ═══════ 操作方法 ════════
    void SetCurrentTenant(Guid? tenantId);
    void SetCurrentTenants(IReadOnlyList<Guid>? tenantIds);
    bool HasAccess(Guid tenantId);
}
```

### 租户属性对照表

| 属性 | 租户用户 | 平台用户 |
|------|---------|---------|
| `TenantId` | User.TenantId | null |
| `TenantIds` | [] | PlatformUserTenants 分配列表 |
| `CurrentTenantId` | TenantId | SetCurrentTenant 设置值 / null |
| `CurrentTenantIds` | [TenantId] | SetCurrentTenants 设置值 / [CurrentTenantId] / TenantIds |

### 数据隔离逻辑

```csharp
// Service 层统一使用 CurrentTenantIds
var query = _uow.Repository<Order>().FindAsync(
    o => _currentUser.CurrentTenantIds.Contains(o.TenantId));
```

### Redis 缓存策略

| Key | TTL | 说明 |
|-----|-----|------|
| `user:tenant:{userId}` | 30min | 租户信息缓存 |

**缓存失效时机**：用户 TenantId 变更、PlatformUserTenants 分配变更

## 登录锁定 / Login Lockout

| 参数 | 值 | 说明 |
|------|-----|------|
| MaxFailedAttempts | 5 | 连续失败次数阈值 |
| LockoutMinutes | 5 | 锁定持续时间 |

## 密码修改 / Change Password

```
POST /api/auth/change-password (Authorized)
  ├─ 验证当前密码
  ├─ BCrypt.HashPassword(new) → 更新 PasswordHash
  ├─ SecurityStamp = Guid.NewGuid() → 旧 token 即时失效
  └─ 返回成功
```

## API 端点 / API Endpoints

| 方法 | 路径 | 认证 | 说明 |
|------|------|------|------|
| `POST` | `/api/auth/login` | 无 | 登录，返回 JWT |
| `GET` | `/api/auth/profile` | Bearer | 当前用户资料 |
| `POST` | `/api/auth/change-password` | Bearer | 修改密码 |
| `GET` | `/api/auth/permissions` | Bearer | 当前用户权限码列表 |

## 种子数据 / Seed Data

首次启动自动创建：

| 类型 | 数据 | UserType |
|------|------|----------|
| 管理员 | `admin` / `Admin@123` | PlatformAdmin |
| 测试用户 | `testuser` / `Test@123` | TenantUser |