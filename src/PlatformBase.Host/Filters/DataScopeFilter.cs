using Microsoft.AspNetCore.Mvc.Filters;       // 提供 IAsyncActionFilter / ActionExecutingContext
using PlatformBase.Core.Services;              // 提供 ICurrentUserContext 用户会话上下文

namespace PlatformBase.Host.Filters;

/// <summary>
/// 数据权限 ActionFilter — 根据用户所属部门物化路径限制数据可见范围
/// 当前为预留框架，平台管理员不过滤
///
/// 实现逻辑（当前为 PLACEHOLDER，预留完整实现）：
///   1. 检查 Action 是否标注了 [DataScope] Attribute → 无标记则跳过
///   2. 平台管理员（IsSuperAdmin）→ 默认不过滤，可查看所有数据
///   3. 普通用户 → 预留：通过 ICurrentUserContext.CurrentTenantId / AccessibleTenantIds 限制租户范围
///   4. 未来扩展：基于 OrganizationUnit.Path（部门物化路径）实现行级过滤
///      - 将 DataScope 条件注入到 Action 参数（如追加 Expression<Func<T, bool>>）
///      - 或通过 HttpContext.Items 传递过滤条件，由 Repository 层读取应用
/// </summary>
public class DataScopeFilter : IAsyncActionFilter
{
    /// <summary>当前用户会话上下文，提供租户/部门信息</summary>
    private readonly ICurrentUserContext _currentUser;

    /// <summary>构造函数：注入 ICurrentUserContext</summary>
    public DataScopeFilter(ICurrentUserContext currentUser)
    {
        _currentUser = currentUser; // 保存用户服务引用
    }

    /// <summary>
    /// Action 执行前的数据权限拦截
    /// 当前仅做标记检查，平台管理员直接放行
    /// </summary>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 查找 Action 上是否标注了 [DataScope] Attribute
        var attr = context.ActionDescriptor.EndpointMetadata
            .OfType<DataScopeAttribute>().FirstOrDefault(); // 过滤 DataScopeAttribute 类型，取第一个
        if (attr == null) { await next(); return; } // 无标记 → 不需要数据过滤，直接执行后续 Action

        // 平台管理员不过滤，后续可基于 OrganizationUnit.Path 实现行级过滤
        // TODO: 按 ICurrentUserContext.AccessibleTenantIds 注入租户过滤条件
        // TODO: 按 OrganizationUnit.Path LIKE '部门路径%' 实现部门级行级过滤
        await next(); // 继续执行 Action（当前无过滤逻辑）
    }
}
