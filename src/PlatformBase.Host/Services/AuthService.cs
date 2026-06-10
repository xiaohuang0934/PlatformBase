using System.Security.Claims;
using System.Security.Cryptography;
using IdentityServer4;
using IdentityServer4.Models;
using PlatformBase.Application.Services;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;

namespace PlatformBase.Host.Services;

/// <summary>
/// 认证授权服务实现，封装完整的登录、Token 刷新、密码修改流程
/// 依赖 IUserService 完成用户数据操作，依赖 IdentityServerTools 签发 JWT，
/// 依赖 PersistedGrantStore 管理 RefreshToken 持久化
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserService _userService;
    private readonly IdentityServerTools _identityServerTools;
    private readonly IdentityServer.PersistedGrantStore _grantStore;

    private const string ClientId = "platformbase-client";
    private const int AccessTokenLifetime = 300;

    public AuthService(
        IUserService userService,
        IdentityServerTools identityServerTools,
        IdentityServer.PersistedGrantStore grantStore)
    {
        _userService = userService;
        _identityServerTools = identityServerTools;
        _grantStore = grantStore;
    }

    /// <inheritdoc />
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userService.GetByUsernameAsync(request.Username, cancellationToken);
        if (user == null)
            throw new BusinessException("用户名或密码错误", ErrorCode.UserNotFound);

        if (!user.IsActive)
            throw new BusinessException("账户已被禁用", ErrorCode.UserLocked);

        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
            throw new BusinessException("账户已锁定，请稍后重试", ErrorCode.UserLocked);

        var passwordValid = await _userService.CheckPasswordAsync(user.Id, request.Password, cancellationToken);
        if (!passwordValid)
        {
            await _userService.RecordLoginFailedAsync(user.Id, cancellationToken);
            throw new BusinessException("用户名或密码错误", ErrorCode.PasswordMismatch);
        }

        await _userService.RecordLoginSuccessAsync(user.Id, cancellationToken);
        var roles = await _userService.GetRolesAsync(user.Id, cancellationToken);
        var (accessToken, refreshToken) = await IssueTokensAsync(user.Id, user.Username, user.SecurityStamp,
            user.Email, roles);

        return new LoginResponse
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = AccessTokenLifetime,
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

        var roles = await _userService.GetRolesAsync(user.Id, cancellationToken);
        var (newAccessToken, newRefreshToken) = await IssueTokensAsync(user.Id, user.Username,
            user.SecurityStamp, user.Email, roles);

        return new LoginResponse
        {
            AccessToken = newAccessToken,
            TokenType = "Bearer",
            ExpiresIn = AccessTokenLifetime,
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
            throw new BusinessException("当前密码错误", ErrorCode.PasswordMismatch);

        var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        var newStamp = Guid.NewGuid().ToString();

        await _userService.UpdatePasswordAsync(user.Id, newHash, newStamp, cancellationToken);
        await _grantStore.RevokeUserTokensAsync(user.Id.ToString());
    }

    /// <summary>
    /// 签发 AccessToken + RefreshToken（组合操作）
    /// </summary>
    private async Task<(string accessToken, string refreshToken)> IssueTokensAsync(
        Guid userId, string username, string securityStamp, string? email, IReadOnlyList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new("security_stamp", securityStamp),
            new("aud", "api1")
        };

        if (!string.IsNullOrEmpty(email))
            claims.Add(new Claim(ClaimTypes.Email, email));

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var accessToken = await _identityServerTools.IssueJwtAsync(
            lifetime: AccessTokenLifetime, claims: claims);

        var refreshToken = GenerateTokenValue();
        await _grantStore.StoreAsync(new PersistedGrant
        {
            Key = refreshToken,
            Type = "refresh_token",
            SubjectId = userId.ToString(),
            ClientId = ClientId,
            CreationTime = DateTime.UtcNow,
            Expiration = DateTime.UtcNow.AddDays(30),
            Data = "{}"
        });

        return (accessToken, refreshToken);
    }

    /// <summary>生成安全的随机令牌值</summary>
    private static string GenerateTokenValue()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
