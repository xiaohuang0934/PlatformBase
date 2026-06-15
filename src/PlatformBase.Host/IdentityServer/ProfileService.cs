using System.Security.Claims;
using IdentityServer4.Extensions;
using IdentityServer4.Models;
using IdentityServer4.Services;

namespace PlatformBase.Host.IdentityServer;

/// <summary>
/// IdentityServer4 自定义 ProfileService
/// 在签发 JWT Token 和验证 UserInfo 端点时，动态注入用户的角色、安全戳等 Claims
/// 安全戳存储在 JWT 中，下游服务可验证用户凭证是否已变更（如密码修改）
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
    /// 在签发 Token 时调用，向 JWT 中注入额外的用户身份信息
    /// 注入的 Claims 包括：安全戳
    /// </summary>
    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        var userId = context.Subject.GetSubjectId();
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

        var claims = new List<Claim>(context.Subject.Claims)
        {
            new("security_stamp", user.SecurityStamp),
            new("user_type", ((int)user.UserType).ToString()),
            new("super_admin", user.IsSuperAdmin ? "1" : "0")
        };

        _logger.LogWarning("ProfileService.GetProfileData: user={User}, superAdmin={SuperAdmin}, reqTypes={Types}",
            user.Username, user.IsSuperAdmin,
            string.Join(",", context.RequestedClaimTypes));

        context.IssuedClaims = claims;

        context.IssuedClaims = claims;
    }

    /// <summary>
    /// 验证用户是否仍有效（未被停用、未被软删除）
    /// 每次 Token 验证时 IdentityServer 会调用此方法
    /// </summary>
    public async Task IsActiveAsync(IsActiveContext context)
    {
        var userId = context.Subject.GetSubjectId();
        if (!Guid.TryParse(userId, out var guid))
        {
            context.IsActive = false;
            return;
        }

        // 注意：GetByIdAsync 通过全局过滤器自动排除已软删除的用户
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
