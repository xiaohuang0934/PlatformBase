using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;

namespace PlatformBase.Host.Extensions;

/// <summary>
/// Hangfire Dashboard 授权过滤器，仅允许 Admin 角色访问
/// </summary>
public class HangfireAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext.User.Identity?.IsAuthenticated != true)
            return false;
        return httpContext.User.IsInRole("Admin");
    }
}
