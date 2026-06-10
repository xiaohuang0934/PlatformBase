using System.Security.Claims;
using IdentityModel;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using PlatformBase.Application.Services;
using PlatformBase.Core.Exceptions;

namespace PlatformBase.Host.IdentityServer;

/// <summary>
/// IdentityServer4 密码授权模式的自定义验证器
/// 替代默认的 ASP.NET Core Identity 集成，直接对接自建的 User/Role 实体体系
/// 验证流程：查用户 → 检查锁定/停用状态 → BCrypt 密码验证 → 登录追踪 → 签发 Claims
/// </summary>
public class ResourceOwnerPasswordValidator : IResourceOwnerPasswordValidator
{
    private readonly IUserService _userService;
    private readonly ILogger<ResourceOwnerPasswordValidator> _logger;

    public ResourceOwnerPasswordValidator(IUserService userService,
        ILogger<ResourceOwnerPasswordValidator> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// 验证用户名和密码，验证通过后返回包含用户身份信息（Subject + Claims）的授权结果
    /// </summary>
    public async Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
    {
        try
        {
            // ① 根据用户名查找用户
            var user = await _userService.GetByUsernameAsync(context.UserName);
            if (user == null)
            {
                _logger.LogWarning("登录失败：用户 {Username} 不存在", context.UserName);
                context.Result = new GrantValidationResult(
                    TokenRequestErrors.InvalidGrant, "用户名或密码错误");
                return;
            }

            // ② 检查账户是否被锁定
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

            // ③ 检查账户是否被停用
            if (!user.IsActive)
            {
                _logger.LogWarning("登录失败：用户 {Username} 已被禁用", user.Username);
                context.Result = new GrantValidationResult(
                    TokenRequestErrors.InvalidGrant, "账户已被禁用");
                return;
            }

            // ④ BCrypt 密码验证
            var passwordValid = await _userService.CheckPasswordAsync(user.Id, context.Password);
            if (!passwordValid)
            {
                await _userService.RecordLoginFailedAsync(user.Id);
                _logger.LogWarning("登录失败：用户 {Username} 密码错误（失败次数：{Count}）",
                    user.Username, user.AccessFailedCount + 1);
                context.Result = new GrantValidationResult(
                    TokenRequestErrors.InvalidGrant, "用户名或密码错误");
                return;
            }

            // ⑤ 登录成功，记录成功状态（清零失败次数、解锁）
            await _userService.RecordLoginSuccessAsync(user.Id);
            _logger.LogInformation("登录成功：用户 {Username} ({UserId})", user.Username, user.Id);

            // ⑥ 获取用户角色
            var roles = await _userService.GetRolesAsync(user.Id);

            // ⑦ 构建 Claims 并签发令牌
            var claims = new List<Claim>
            {
                new(JwtClaimTypes.Subject, user.Id.ToString()),
                new(JwtClaimTypes.Name, user.Username),
                new("security_stamp", user.SecurityStamp)
            };

            if (!string.IsNullOrEmpty(user.Email))
                claims.Add(new Claim(JwtClaimTypes.Email, user.Email));

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
}
