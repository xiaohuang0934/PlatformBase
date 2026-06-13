# PlatformBase JWT + IdentityServer4 认证授权 — 手把手教学笔记

> **关联文档：**
> - [03-多数据库配置](./03-多数据库配置.md) — AppDbContext / UnitOfWork
> - [02-用户会话上下文](./02-用户会话上下文.md) — ICurrentUserContext 封装的 Claims 提取
> - [07a-IS4组件-RedisPersistedGrantStore](./07a-IS4组件-RedisPersistedGrantStore.md) — Redis 实现的 refresh_token 持久化存储
> - [13-Redis缓存](./13-Redis缓存.md) — Redis 缓存架构与降级策略

---

## 一、认证授权全景图

```
┌──────────────────────────────────────────────────────────────────────┐
│                        认证授权全链路                                  │
├──────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ① 登录: POST /connect/token  (grant_type=password)                  │
│        │                                                             │
│        ▼                                                             │
│  ┌─────────────────────────┐   ┌──────────────────┐   ┌───────────┐ │
│  │ ResourceOwnerPassword-  │──▶│ ProfileService    │──▶│ Persisted │ │
│  │ Validator               │   │ 补充 Claims:      │   │ GrantStore│ │
│  │ (本章完整实现)           │   │ 角色/安全戳       │   │ (见07a)  │ │
│  └─────────────────────────┘   └──────────────────┘   └───────────┘ │
│                                                                      │
│  ② 后续请求: Authorization: Bearer {token}                           │
│        │                                                             │
│        ▼                                                             │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │ JwtBearer 中间件:                                             │  │
│  │  a. 签名校验 (RSA SHA256) — 与 IS4 共用同一个 X509 密钥          │  │
│  │  b. 签发方校验  (Issuer)                                       │  │
│  │  c. 受众校验    (Audience)                                    │  │
│  │  d. 有效期校验  (exp - nbf + 1分钟ClockSkew)                   │  │
│  │  e. 填充 HttpContext.User (ClaimsPrincipal)                   │  │
│  └───────────────────────────────────────────────────────────────┘  │
│        │                                                             │
│        ▼                                                             │
│  ┌──────────────────┐    ┌──────────────────┐    ┌───────────────┐ │
│  │ StampValidation  │───▶│ PermissionPolicy │───▶│ Permission-  │ │
│  │ Middleware       │    │ Provider         │    │ AuthHandler  │ │
│  │ 安全戳即时失效    │    │ 动态生成策略     │    │ 权限校验     │ │
│  └──────────────────┘    └──────────────────┘    └───────────────┘ │
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 二、Program.cs 中的 IS4 + JWT 注册（完整）

```csharp
// ═══ 签名密钥 — X509 自签名证书（跨平台兼容）═══
using var rsa = RSA.Create(2048);
var certRequest = new CertificateRequest(
    "CN=PlatformBase", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
var cert = certRequest.CreateSelfSigned(
    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
var signingKey = new X509SecurityKey(cert) { KeyId = Guid.NewGuid().ToString() };

// ═══ IdentityServer4 — Token 签发服务 ═══
builder.Services.AddIdentityServer(options =>
{
    options.Events.RaiseErrorEvents = true;
    options.Events.RaiseSuccessEvents = true;
    options.Events.RaiseFailureEvents = true;
    options.Events.RaiseInformationEvents = true;
})
.AddResourceOwnerValidator<ResourceOwnerPasswordValidator>()  // 密码验证
.AddProfileService<ProfileService>()                            // Claims 补充
.AddSigningCredential(new SigningCredentials(
    signingKey, SecurityAlgorithms.RsaSha256))
.AddPersistedGrantStore<PersistedGrantStore>()    // refresh_token 持久化
.AddInMemoryApiResources(Config.ApiResources)
.AddInMemoryApiScopes(Config.ApiScopes)
.AddInMemoryIdentityResources(Config.IdentityResources)
.AddInMemoryClients(Config.Clients);

// ═══ JWT Bearer — Token 验证（与 IS4 共享签名密钥）═══
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = signingKey,
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["IdentityServer:Authority"]
            ?? "http://localhost:5269",
        ValidateAudience = true,
        ValidAudience = "api1",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
    };

    // 认证失败 → 统一 ApiResult 格式
    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = 200;
            var result = ApiResult.Fail(ErrorCode.Unauthorized, "认证失败，请重新登录");
            await context.Response.WriteAsync(JsonSerializer.Serialize(result, camelCase));
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = 200;
            var result = ApiResult.Fail(ErrorCode.Forbidden, "没有访问权限");
            await context.Response.WriteAsync(JsonSerializer.Serialize(result, camelCase));
        }
    };
});

// ═══ 权限策略 = ═══
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
```

---

## 三、ResourceOwnerPasswordValidator（完整实现）

```csharp
// PlatformBase.Host/IdentityServer/ResourceOwnerPasswordValidator.cs
using System.Security.Claims;
using IdentityModel;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using PlatformBase.Application.Services;
using PlatformBase.Core.Exceptions;
using StackExchange.Redis;

/// <summary>
/// IdentityServer4 密码授权模式的自定义验证器。
/// 替代默认的 ASP.NET Core Identity 集成，直接对接自建的 User/Role 实体体系。
/// 
/// 验证流程（6 步）：
///   ① Redis 用户名维度频控（5 分钟内最多 10 次失败）
///   ② 根据用户名查 User 表
///   ③ 检查账户锁定状态（LockoutEnd）
///   ④ 检查账户启用状态（IsActive）
///   ⑤ BCrypt 密码验证
///   ⑥ 登录成功 → 清除频控计数 → 获取角色 → 签发 Claims
/// </summary>
public class ResourceOwnerPasswordValidator : IResourceOwnerPasswordValidator
{
    private readonly IUserService _userService;
    private readonly ILogger<ResourceOwnerPasswordValidator> _logger;
    private readonly IDatabase? _redis;         // 可降级（null = Redis 不可用）

    private const int MaxFailPerUser = 10;       // 窗口内最大失败次数
    private const int RateLimitWindowMinutes = 5; // 频控窗口长度

    public ResourceOwnerPasswordValidator(
        IUserService userService,
        ILogger<ResourceOwnerPasswordValidator> logger,
        IServiceProvider serviceProvider)        // IServiceProvider 延迟获取 Redis
    {
        _userService = userService;
        _logger = logger;
        // GetService（非 Required）→ 不存在时 = null
        _redis = serviceProvider.GetService<IConnectionMultiplexer>()?.GetDatabase();
    }

    public async Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
    {
        try
        {
            var normalizedUser = Normalize(context.UserName);

            // ① Redis 频控：用户名维度
            if (_redis != null)
            {
                var failKey = $"login:fail:{normalizedUser}";
                var failCount = await GetFailCountAsync(failKey);
                if (failCount >= MaxFailPerUser)
                {
                    context.Result = new GrantValidationResult(
                        TokenRequestErrors.InvalidGrant,
                        $"登录尝试次数过多，请 {RateLimitWindowMinutes} 分钟后重试");
                    return;
                }
            }

            // ② 查用户
            var user = await _userService.GetByUsernameAsync(context.UserName);
            if (user == null)
            {
                _logger.LogWarning("登录失败：用户 {Username} 不存在", context.UserName);
                await IncrementFailCountAsync($"login:fail:{normalizedUser}");
                context.Result = new GrantValidationResult(
                    TokenRequestErrors.InvalidGrant, "用户名或密码错误");
                return;
            }

            // ③ 检查锁定状态
            if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
            {
                var remaining = user.LockoutEnd.Value - DateTimeOffset.UtcNow;
                _logger.LogWarning("登录失败：用户 {Username} 已被锁定，剩余 {Minutes} 分钟",
                    user.Username, Math.Ceiling(remaining.TotalMinutes));
                context.Result = new GrantValidationResult(
                    TokenRequestErrors.InvalidGrant,
                    $"账户已被锁定，请 {Math.Ceiling(remaining.TotalMinutes)} 分钟后重试");
                return;
            }

            // ④ 检查启用状态
            if (!user.IsActive)
            {
                _logger.LogWarning("登录失败：用户 {Username} 已被禁用", user.Username);
                context.Result = new GrantValidationResult(
                    TokenRequestErrors.InvalidGrant, "账户已被禁用");
                return;
            }

            // ⑤ BCrypt 密码验证
            var passwordValid = await _userService.CheckPasswordAsync(user.Id, context.Password);
            if (!passwordValid)
            {
                await _userService.RecordLoginFailedAsync(user.Id);
                await IncrementFailCountAsync($"login:fail:{normalizedUser}");
                _logger.LogWarning("登录失败：用户 {Username} 密码错误（失败次数：{Count}）",
                    user.Username, user.AccessFailedCount + 1);
                context.Result = new GrantValidationResult(
                    TokenRequestErrors.InvalidGrant, "用户名或密码错误");
                return;
            }

            // ⑥ 登录成功：清除频控 + 记录成功 + 获取角色 + 签发
            await _userService.RecordLoginSuccessAsync(user.Id);
            await DeleteFailCountAsync($"login:fail:{normalizedUser}");
            _logger.LogInformation("登录成功：用户 {Username} ({UserId})", user.Username, user.Id);

            var roles = await _userService.GetRolesAsync(user.Id);

            var claims = new List<Claim>
            {
                new(JwtClaimTypes.Subject, user.Id.ToString()),
                new(JwtClaimTypes.Name, user.Username),
                new("security_stamp", user.SecurityStamp)
            };
            if (!string.IsNullOrEmpty(user.Email))
                claims.Add(new Claim(JwtClaimTypes.Email, user.Email));

            claims.Add(new Claim("tenant_id", user.TenantId?.ToString() ?? ""));
            claims.Add(new Claim("user_type", ((int)user.UserType).ToString()));
            claims.AddRange(roles.Select(role => new Claim(JwtClaimTypes.Role, role)));

            context.Result = new GrantValidationResult(
                subject: user.Id.ToString(),
                authenticationMethod: "password",
                claims: claims);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登录验证异常");
            context.Result = new GrantValidationResult(
                TokenRequestErrors.InvalidGrant, "登录服务异常，请稍后重试");
        }
    }

    // ─── Redis 频控辅助方法 ───
    private async Task<int> GetFailCountAsync(string key)
    {
        if (_redis == null) return 0;
        try
        {
            var value = await _redis.StringGetAsync(key);
            return value.HasValue && int.TryParse(value, out var count) ? count : 0;
        }
        catch { return 0; }  // Redis 不可用 → 跳过频控
    }

    private async Task IncrementFailCountAsync(string key)
    {
        if (_redis == null) return;
        try
        {
            await _redis.StringIncrementAsync(key);
            await _redis.KeyExpireAsync(key, TimeSpan.FromMinutes(RateLimitWindowMinutes));
        }
        catch { /* Redis 不可用 → 降级跳过 */ }
    }

    private async Task DeleteFailCountAsync(string key)
    {
        if (_redis == null) return;
        try { await _redis.KeyDeleteAsync(key); }
        catch { /* Redis 不可用 → 降级跳过 */ }
    }

    private static string Normalize(string value)
        => PlatformBase.Core.Extensions.StringExtensions.Normalize(value);
}
```

---

## 四、ProfileService（完整实现）

```csharp
// PlatformBase.Host/IdentityServer/ProfileService.cs
using System.Security.Claims;
using IdentityServer4.Extensions;
using IdentityServer4.Models;
using IdentityServer4.Services;
using PlatformBase.Application.Services;

/// <summary>
/// IdentityServer4 自定义 ProfileService。
/// 在签发 JWT Token 和验证 UserInfo 端点时，动态注入用户的角色、安全戳等 Claims。
/// </summary>
public class ProfileService : IProfileService
{
    private readonly IUserService _userService;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(IUserService userService, ILogger<ProfileService> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// 在签发 Token 时被调用。向 JWT 中注入额外的 Claims：角色列表 + 安全戳。
    /// </summary>
    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        var userId = context.Subject.GetSubjectId();         // 从当前请求的 Subject 提取 userId
        if (!Guid.TryParse(userId, out var guid))
        {
            _logger.LogWarning("GetProfileData: 无法解析 SubjectId {SubjectId}", userId);
            return;
        }

        var user = await _userService.GetByIdAsync(guid);
        if (user == null)
        {
            _logger.LogWarning("GetProfileData: 用户 {UserId} 不存在", userId);
            return;
        }

        // 保留 ResourceOwnerPasswordValidator 中已注入的 Claims，再追加新 Claim
        var claims = new List<Claim>(context.Subject.Claims)
        {
            new("security_stamp", user.SecurityStamp)          // 安全戳：密码变更后比对
        };

        // 注入角色 Claims
        var roles = await _userService.GetRolesAsync(guid);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        _logger.LogDebug("GetProfileData: 用户 {Username} 角色={Roles}",
            user.Username, string.Join(",", roles));

        context.IssuedClaims = claims;   // 设置最终签发的 Claims
    }

    /// <summary>
    /// 验证用户是否仍有效（未被停用、未被软删除）。
    /// 每次 Token 验证时 IdentityServer 会调用此方法。
    /// </summary>
    public async Task IsActiveAsync(IsActiveContext context)
    {
        var userId = context.Subject.GetSubjectId();
        if (!Guid.TryParse(userId, out var guid))
        {
            context.IsActive = false;
            return;
        }

        // GetByIdAsync 通过全局过滤器自动排除已软删除的用户
        var user = await _userService.GetByIdAsync(guid);
        if (user == null || !user.IsActive)
        {
            _logger.LogDebug("IsActive: 用户 {UserId} 不可用 (不存在或已停用)", userId);
            context.IsActive = false;
            return;
        }

        context.IsActive = true;
    }
}
```

---

## 五、RefreshToken 持久化 — RedisPersistedGrantStore

> **完整实现参见独立文档：** [07a-IS4组件-RedisPersistedGrantStore](./07a-IS4组件-RedisPersistedGrantStore.md)

核心设计：

```
grant:{key}                     → JSON (TTL = Expiration)
grant:idx:{type}:{subjectId}    → Set (按用户索引)

StoreAsync:  SET + SADD  (1 次 Pipeline)
GetAsync:    GET  (<0.5ms)
GetAllAsync: SMEMBERS + MGET  (2 次命令)
RemoveAsync: DEL + SREM  (1 次 Pipeline)
```

---

## 六、安全戳验证 — 密码变更即时失效

```
用户修改密码
  → UserService 更新 SecurityStamp 字段
  → 同时删除 Redis stamp:{userId} 缓存

旧 Token 下一次请求
  → StampValidationMiddleware
  → 提取 security_stamp Claim
  → Redis GET stamp:{userId}
     HIT → 对比 → 不匹配 → 返回 401
     MISS → DB SELECT → 对比 → 回写 Redis (TTL=180s)
  → 结果：最多延迟 180 秒旧 Token 即失效
```

---

## 七、登录完整流程图

```
POST /connect/token
  grant_type=password&username=admin&password=xxx&client_id=web
    │
    ▼
ResourceOwnerPasswordValidator.ValidateAsync()
    │
    ├── ① Redis 频率检查：login:fail:{username}  > 10? → 拒绝
    ├── ② UserService.GetByUsernameAsync()       → 用户存在?
    ├── ③ user.LockoutEnd > now?                 → 锁定中? → 拒绝
    ├── ④ user.IsActive == true?                 → 已停用? → 拒绝
    ├── ⑤ BCrypt.Verify(password, hash)          → 密码正确?
    ├── ⑥ RecordLoginSuccessAsync() + 清除频控
    ├── ⑦ GetRolesAsync() → 角色列表
    └── ⑧ 构建 Claims → GrantValidationResult(subject, "password", claims)
          │
          ▼
ProfileService.GetProfileDataAsync()
    │
    ├── 补充 security_stamp Claim (当前 DB 中的值)
    ├── 补充角色 Claims
    └── context.IssuedClaims = claims
          │
          ▼
PersistedGrantStore.StoreAsync()
    │
    ├── SET grant:{key} JSON (TTL=expire)
    └── SADD grant:idx:{type}:{subId} {key}
          │
          ▼
返回: { access_token, refresh_token, expires_in, token_type }
```

---

## 八、令牌刷新流程

```
POST /connect/token
  grant_type=refresh_token&refresh_token=xxx&client_id=web
    │
    ▼
IdentityServer4 内部处理
    │
    ├── PersistedGrantStore.GetAsync(refresh_token_key)
    │   → Redis GET grant:{key} → 反序列化 PersistedGrant
    ├── 验证 ClientId / 过期时间
    ├── 生成新的 access_token + refresh_token
    ├── 删除旧的 grant → RemoveAsync(old_key)
    ├── 存储新的 grant → StoreAsync(new_grant)
    │
    ▼
返回: { access_token (新), refresh_token (新), expires_in, token_type }
```

---

## 九、中间件管道顺序

```csharp
app.UseMiddleware<GlobalExceptionMiddleware>();    // ① 异常兜底
app.UseSerilogRequestLogging();                     // ② 请求日志
app.UseRequestLocalization();                       // ③ 国际化
app.UseRouting();                                   // ④ 路由匹配
app.UseCors();                                      // ⑤ 跨域
app.UseIdentityServer();                            // ⑥ IS4 端点 (/connect/token)
app.UseAuthentication();                            // ⑦ JWT 验证
app.UseMiddleware<StampValidationMiddleware>();     // ⑧ 安全戳
app.UseAuthorization();                             // ⑨ 权限鉴权
app.MapControllers();                               // ⑩ 控制器
```

---

## 十、最佳实践速查卡

```
┌─────────────────────────────────────────────────────────────────┐
│         JWT + IdentityServer4 认证授权 黄金法则                   │
├─────────────────────────────────────────────────────────────────┤
│  1. 签发和验签共用同一个 X509 密钥（无需 HTTP 发现）              │
│  2. RSA 2048 + SHA256 + Pkcs1（跨平台兼容，含 macOS）            │
│  3. ResourceOwnerPasswordValidator 使用 IServiceProvider 延迟    │
│     获取 Redis（避免 DI 链崩溃）                                 │
│  4. 登录频控用 Redis StringIncrement + EXPIRE（降级跳过）         │
│  5. BCrypt 验证用户密码（哈希不可逆）                             │
│  6. ProfileService 补充 security_stamp 用于下游安全戳验证        │
│  7. refresh_token 持久化到 Redis（Pipeline 批量提交）             │
│  8. 安全戳保证密码变更后旧 Token 即时失效（最多 180s 延迟）       │
│  9. HTTP 200 + ApiResult 格式统一所有认证/鉴权失败的响应           │
│ 10. OnChallenge → 401, OnForbidden → 403, Exception → 500        │
└─────────────────────────────────────────────────────────────────┘
```
