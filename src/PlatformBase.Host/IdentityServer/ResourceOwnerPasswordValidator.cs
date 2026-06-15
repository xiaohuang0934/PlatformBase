using System.Security.Claims;
using IdentityModel;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using StackExchange.Redis;
using StringExtensions = PlatformBase.Core.Extensions.StringExtensions;

namespace PlatformBase.Host.IdentityServer;

/// <summary>
/// IdentityServer4 密码授权模式的自定义验证器
/// 替代默认的 ASP.NET Core Identity 集成，直接对接自建的 User/Role 实体体系
/// 验证流程：Redis 频控 → 查用户 → 检查锁定/停用 → BCrypt 密码验证 → 登录追踪 → 签发 Claims
/// </summary>
public class ResourceOwnerPasswordValidator : IResourceOwnerPasswordValidator
{
    private readonly IUserService _userService;
    private readonly ILogger<ResourceOwnerPasswordValidator> _logger;
    private readonly IDatabase? _redis;

    private const int MaxFailPerUser = 10;
    private const int RateLimitWindowMinutes = 5;

    public ResourceOwnerPasswordValidator(
        IUserService userService,
        ILogger<ResourceOwnerPasswordValidator> logger,
        IServiceProvider serviceProvider)
    {
        _userService = userService;
        _logger = logger;
        _redis = serviceProvider.GetService<IConnectionMultiplexer>()?.GetDatabase();
    }

    /// <summary>
    /// 验证用户名和密码，验证通过后返回包含用户身份信息（Subject + Claims）的授权结果
    /// </summary>
    public async Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
    {
        try
        {
            var normalizedUser = Normalize(context.UserName);

            // ① Redis 频控：用户名维度，5 分钟内最多 10 次失败
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

            // ② 根据用户名查找用户
            var user = await _userService.GetByUsernameAsync(context.UserName);
            if (user == null)
            {
                _logger.LogWarning("登录失败：用户 {Username} 不存在", context.UserName);
                await IncrementFailCountAsync($"login:fail:{normalizedUser}");
                context.Result = new GrantValidationResult(
                    TokenRequestErrors.InvalidGrant, "用户名或密码错误");
                return;
            }

            // ③ 检查账户是否被锁定
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

            // ④ 检查账户是否被停用
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

            // ⑥ 登录成功：记录成功状态 + 清除频控计数
            await _userService.RecordLoginSuccessAsync(user.Id);
            await DeleteFailCountAsync($"login:fail:{normalizedUser}");
            _logger.LogInformation("登录成功：用户 {Username} ({UserId})", user.Username, user.Id);

            // ⑦ 获取用户角色
            var roles = await _userService.GetRolesAsync(user.Id);

            // ⑧ 构建 Claims 并签发令牌
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

    private async Task<int> GetFailCountAsync(string key)
    {
        if (_redis == null) return 0;
        try
        {
            var value = await _redis.StringGetAsync(key);
            return value.HasValue && int.TryParse(value, out var count) ? count : 0;
        }
        catch { return 0; }
    }

    private async Task IncrementFailCountAsync(string key)
    {
        if (_redis == null) return;
        try
        {
            await _redis.StringIncrementAsync(key);
            await _redis.KeyExpireAsync(key, TimeSpan.FromMinutes(RateLimitWindowMinutes));
        }
        catch { /* Redis 不可用，降级跳过 */ }
    }

    private async Task DeleteFailCountAsync(string key)
    {
        if (_redis == null) return;
        try { await _redis.KeyDeleteAsync(key); }
        catch { /* Redis 不可用，降级跳过 */ }
    }

    private static string Normalize(string value) => StringExtensions.Normalize(value);
}
