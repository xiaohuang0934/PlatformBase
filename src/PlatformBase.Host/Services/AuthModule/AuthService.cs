using System.Security.Claims;
using System.Security.Cryptography;
using IdentityServer4;
using IdentityServer4.Models;
using Microsoft.Extensions.Localization;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Extensions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Repositories;
using PlatformBase.Host.IdentityServer;
using PlatformBase.Host.Resources;
using StackExchange.Redis;

namespace PlatformBase.Host.Services.AuthModule;

/// <summary>
/// 认证授权服务实现，封装完整的登录、Token 刷新、密码修改流程
/// 依赖 IUserService 完成用户数据操作，依赖 IdentityServerTools 签发 JWT，
/// 依赖 PersistedGrantStore 管理 RefreshToken 持久化
/// Redis 频控 + 密码变更时失效 stomp 缓存
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserService _userService;
    private readonly IdentityServerTools _identityServerTools;
    private readonly PersistedGrantStore _grantStore;
    private readonly IUnitOfWork _uow;
    private readonly IDatabase? _redis;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ISystemParamService _sysParam;

    private const string ClientId = "platformbase-client";
    private const int DefaultAccessTokenLifetime = 300;
    private const int DefaultRefreshTokenDays = 30;
    private const int MaxFailPerUser = 10;
    private const int RateLimitWindowMinutes = 5;

    public AuthService(
        IUserService userService,
        IdentityServerTools identityServerTools,
        PersistedGrantStore grantStore,
        IUnitOfWork uow,
        IServiceProvider serviceProvider,
        IStringLocalizer<SharedResource> localizer,
        ISystemParamService sysParam)
    {
        _userService = userService;
        _identityServerTools = identityServerTools;
        _grantStore = grantStore;
        _uow = uow;
        _redis = serviceProvider.GetService<IConnectionMultiplexer>()?.GetDatabase();
        _localizer = localizer;
        _sysParam = sysParam;
    }

    /// <inheritdoc />
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedUser = Normalize(request.Username);

        // ① 频控：用户名维度
        var failKey = $"login:fail:{normalizedUser}";
        var failCount = await GetFailCountAsync(failKey);
        if (failCount >= MaxFailPerUser)
            throw new BusinessException(_localizer["登录尝试次数过多，请{0}分钟后重试", RateLimitWindowMinutes], ErrorCode.TooManyRequests);

        // ② 查询用户
        var user = await _userService.GetByUsernameAsync(request.Username, cancellationToken);
        if (user == null)
        {
            await IncrementFailCountAsync(failKey);
            throw new BusinessException(_localizer["用户名或密码错误"], ErrorCode.UserNotFound);
        }

        if (!user.IsActive)
            throw new BusinessException(_localizer["账户已被禁用"], ErrorCode.UserLocked);

        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
            throw new BusinessException(_localizer["账户已锁定，请稍后重试"], ErrorCode.UserLocked);

        var passwordValid = await _userService.CheckPasswordAsync(user.Id, request.Password, cancellationToken);
        if (!passwordValid)
        {
            await _userService.RecordLoginFailedAsync(user.Id, cancellationToken);
            await IncrementFailCountAsync(failKey);
            throw new BusinessException(_localizer["用户名或密码错误"], ErrorCode.PasswordMismatch);
        }

        await _userService.RecordLoginSuccessAsync(user.Id, cancellationToken);
        await DeleteFailCountAsync(failKey);
        var (accessToken, refreshToken) = await IssueTokensAsync(user.Id, user.Username, user.SecurityStamp,
            (int)user.UserType, cancellationToken);

        return new LoginResponse
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = await _sysParam.GetValueAsync("access_token_lifetime", DefaultAccessTokenLifetime, cancellationToken),
            RefreshToken = refreshToken
        };
    }

    /// <inheritdoc />
    public async Task<LoginResponse> RefreshTokenAsync(string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var grant = await _grantStore.GetAsync(refreshToken);
        if (grant == null || grant.Type != "refresh_token")
            throw new BusinessException("RefreshToken 无效", ErrorCode.TokenInvalid);

        if (grant.Expiration.HasValue && grant.Expiration < DateTime.UtcNow)
        {
            await _grantStore.RemoveAsync(refreshToken);
            throw new BusinessException("RefreshToken 已过期", ErrorCode.TokenExpired);
        }

        if (!Guid.TryParse(grant.SubjectId, out var userId))
            throw new BusinessException("RefreshToken 数据异常", ErrorCode.TokenInvalid);

        var user = await _userService.GetByIdAsync(userId, cancellationToken);
        if (user == null || !user.IsActive)
        {
            await _grantStore.RemoveAsync(refreshToken);
            throw new BusinessException("用户不存在或已禁用", ErrorCode.UserNotFound);
        }

        await _grantStore.RemoveAsync(refreshToken);

        var (newAccessToken, newRefreshToken) = await IssueTokensAsync(user.Id, user.Username,
            user.SecurityStamp, (int)user.UserType, cancellationToken);

        return new LoginResponse
        {
            AccessToken = newAccessToken,
            TokenType = "Bearer",
            ExpiresIn = await _sysParam.GetValueAsync("access_token_lifetime", DefaultAccessTokenLifetime, cancellationToken),
            RefreshToken = newRefreshToken
        };
    }

    /// <inheritdoc />
    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userService.GetByIdAsync(userId, cancellationToken);
        if (user == null)
            throw new BusinessException("用户不存在", ErrorCode.UserNotFound);

        var valid = await _userService.CheckPasswordAsync(user.Id, currentPassword, cancellationToken);
        if (!valid)
            throw new BusinessException(_localizer["当前密码错误"], ErrorCode.PasswordMismatch);

        var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        var newStamp = Guid.NewGuid().ToString();

        await _uow.BeginTransactionAsync(cancellationToken);
        try
        {
            await _userService.UpdatePasswordAsync(user.Id, newHash, newStamp, cancellationToken);
            await _grantStore.RevokeUserTokensAsync(user.Id.ToString());
            await _uow.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _uow.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        await InvalidateStampCacheAsync(userId);
    }

    /// <summary>
    /// 签发 AccessToken + RefreshToken（组合操作）
    /// </summary>
    private async Task<(string accessToken, string refreshToken)> IssueTokensAsync(
        Guid userId, string username, string securityStamp, int userType, CancellationToken cancellationToken)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new("security_stamp", securityStamp),
            new("aud", "api1"),
            new("user_type", userType.ToString())
        };

        var accessToken = await _identityServerTools.IssueJwtAsync(
            lifetime: await _sysParam.GetValueAsync("access_token_lifetime", DefaultAccessTokenLifetime),
            claims: claims);

        var refreshToken = GenerateTokenValue();
        var rtDays = await _sysParam.GetValueAsync("refresh_token_days", DefaultRefreshTokenDays);
        await _grantStore.StoreAsync(new PersistedGrant
        {
            Key = refreshToken,
            Type = "refresh_token",
            SubjectId = userId.ToString(),
            ClientId = ClientId,
            CreationTime = DateTime.UtcNow,
            Expiration = DateTime.UtcNow.AddDays(rtDays),
            Data = "{}"
        });

        return (accessToken, refreshToken);
    }

    private static string Normalize(string value) => StringExtensions.Normalize(value);

    /// <summary>生成安全的随机令牌值</summary>
    private static string GenerateTokenValue()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
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

    private async Task InvalidateStampCacheAsync(Guid userId)
    {
        if (_redis == null) return;
        try { await _redis.KeyDeleteAsync($"stamp:{userId}"); }
        catch { /* Redis 不可用，降级跳过 */ }
    }
}
