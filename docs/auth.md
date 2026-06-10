# 认证授权体系 / Authentication & Authorization

## 概述 / Overview

PlatformBase 采用 **自建 User 体系 + IdentityServer4 + JWT Bearer** 架构：

- **User 实体** — 继承 `SoftDeleteEntity`，密码使用 BCrypt 哈希
- **IdentityServer4** — OAuth2 Token 签发服务（Password Grant），嵌入式同进程部署
- **JwtBearer** — 验证请求中的 Bearer token（共享 X509 签名密钥）
- **ICurrentUserService** — 注入到 Service / DbContext，提供当前用户会话上下文

## User 实体 / User Entity

```
User : SoftDeleteEntity (继承审计 + 软删除)
  ├ Username / NormalizedUsername      — 登录名 + 大写索引
  ├ Email / NormalizedEmail            — 邮箱 + 大写索引
  ├ PasswordHash                       — BCrypt 哈希（含算法+盐+哈希）
  ├ SecurityStamp                      — 密码变更时更新，旧 token 失效
  ├ LockoutEnd / LockoutEnabled        — 失败锁定
  ├ AccessFailedCount                  — 累计失败次数
  └ IsActive                           — 账号启用/禁用
```

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
  ├─ ⑤ 获取角色 → 构建 Claims
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
    new("aud", "api1"),                    // 必须包含 audience
    new("security_stamp", user.SecurityStamp),
    // + Email + Role claims
};

var token = await _identityServerTools.IssueJwtAsync(3600, claims);
```

**验证端（JwtBearer Middleware）**：

```csharp
.AddJwtBearer(options => {
    options.TokenValidationParameters = new() {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = signingKey,        // X509 公钥
        ValidateIssuer = true,
        ValidIssuer = "http://localhost:5269",
        ValidateAudience = true,
        ValidAudience = "api1",
        ValidateLifetime = true,
    };
});
```

**签名密钥**：IdentityServer 和 JwtBearer 共享同一个 X509 自签名证书（详见 [架构设计](architecture.md)）。

## ICurrentUserService — 会话上下文注入 / Session Context Injection

```csharp
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }
    string? IpAddress { get; }
}
```

**注入链路**：

```
HTTP Request → JwtBearer 验证 → HttpContext.User 填充 (ClaimsPrincipal)
  │
  ├─ Controller / Service
  │     注入 ICurrentUserService
  │     .UserId / .UserName / .Roles / .IsAuthenticated
  │
  └─ AppDbContext.SaveChangesAsync()
         ApplyAuditFields(_currentUserService)
         → CreatedBy / UpdatedBy / DeletedBy 自动填充
```

**使用示例**：

```csharp
public class OrderService
{
    private readonly ICurrentUserService _user;

    public async Task CreateAsync(CreateOrderDto dto)
    {
        var userId = _user.UserId;      // 当前用户 ID
        var username = _user.UserName;  // 当前用户名
        // ...
    }
}
```

## 登录锁定 / Login Lockout

| 参数 | 值 | 说明 |
|------|-----|------|
| MaxFailedAttempts | 5 | 连续失败次数阈值 |
| LockoutMinutes | 5 | 锁定持续时间 |

```csharp
// UserService.RecordLoginFailedAsync
user.AccessFailedCount++;
if (user.AccessFailedCount >= 5)
    user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(5);
// 之后所有登录尝试（即使密码正确）都返回"账户已锁定"
```

**Redis 频控辅助**（Redis 可用时）：

| Key | 维度 | TTL | 作用 |
|-----|------|-----|------|
| `login:fail:{ip}` | IP | 5min | 单 IP 频繁尝试拦截 |
| `login:fail:{username}` | 用户名 | 5min | 单用户名频繁尝试拦截 |

## 密码修改 / Change Password

```
POST /api/auth/change-password (Authorized)
  ├─ 验证当前密码
  ├─ BCrypt.HashPassword(new) → 更新 PasswordHash
  ├─ SecurityStamp = Guid.NewGuid() → 已签发旧 token 逐步失效
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

| 类型 | 数据 |
|------|------|
| 管理员 | `admin` / `Admin@123` → 角色 Admin |
| 测试用户 | `testuser` / `Test@123` → 角色 User |

## Swagger 测试 / Swagger Testing

```
① POST /api/auth/login → {"username":"admin","password":"Admin@123"}
② 复制返回的 accessToken
③ 点击 Authorize 🔒 → 粘贴 token → Authorize
④ 所有端点自动携带 Authorization: Bearer {token}
```
