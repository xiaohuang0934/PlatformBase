using Microsoft.AspNetCore.Authorization;
using PlatformBase.Application.Services;
using PlatformBase.Core.Services;

namespace PlatformBase.Host.Authorization;

/// <summary>
/// 权限鉴权处理器
/// 从 <see cref="ICurrentUserService"/> 获取当前用户，调用 <see cref="IPermissionService"/> 验证权限
/// 鉴权失败时调用 <see cref="AuthorizationHandlerContext.Fail"/> 拒绝请求
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(
        IPermissionService permissionService,
        ICurrentUserService currentUser,
        ILogger<PermissionAuthorizationHandler> logger)
    {
        _permissionService = permissionService;
        _currentUser = currentUser;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == null)
        {
            _logger.LogDebug("权限鉴权失败：用户未认证，请求权限={Permission}", requirement.PermissionCode);
            context.Fail();
            return;
        }

        // 查询用户权限集合（优先 Redis 缓存，缓存未命中查库回写）
        // 异常时标记鉴权失败，不向上抛，确保统一响应格式
        try
        {
            var hasPermission = await _permissionService.HasPermissionAsync(
                _currentUser.UserId.Value, requirement.PermissionCode);

            if (hasPermission)
            {
                _logger.LogDebug("权限鉴权通过：用户={User} 权限={Permission}",
                    _currentUser.UserName, requirement.PermissionCode);
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning("权限鉴权拒绝：用户={User} 权限={Permission}",
                    _currentUser.UserName, requirement.PermissionCode);
                context.Fail();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "权限鉴权异常：用户={User} 权限={Permission}",
                _currentUser.UserName, requirement.PermissionCode);
            context.Fail();
        }
    }
}
