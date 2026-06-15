using System.Security.Claims;
using Hangfire.Dashboard;
using Microsoft.Extensions.DependencyInjection;
using PlatformBase.Application.Services.UserModule;

namespace PlatformBase.Host.Extensions;

/// <summary>
/// Hangfire Dashboard 授权过滤器，仅允许 Admin 角色访问
/// 从数据库查询角色而非依赖 JWT Role Claim，避免 JWT 膨胀
/// </summary>
public class HangfireAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext.User.Identity?.IsAuthenticated != true)
            return false;

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return false;

        var userService = httpContext.RequestServices.GetRequiredService<IUserService>();
        var roles = userService.GetRolesAsync(userId).GetAwaiter().GetResult();
        return roles.Contains("Admin");
    }
}
