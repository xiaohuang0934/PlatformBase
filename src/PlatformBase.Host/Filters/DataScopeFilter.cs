using Microsoft.AspNetCore.Mvc.Filters;
using PlatformBase.Core.Services;

namespace PlatformBase.Host.Filters;

/// <summary>
/// 数据权限 ActionFilter — 根据用户所属部门物化路径限制数据可见范围
/// 当前为预留框架，平台管理员不过滤
/// </summary>
public class DataScopeFilter : IAsyncActionFilter
{
    private readonly ICurrentUserService _currentUser;

    public DataScopeFilter(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var attr = context.ActionDescriptor.EndpointMetadata
            .OfType<DataScopeAttribute>().FirstOrDefault();
        if (attr == null) { await next(); return; }

        // 平台管理员不过滤，后续可基于 OrganizationUnit.Path 实现行级过滤
        await next();
    }
}
