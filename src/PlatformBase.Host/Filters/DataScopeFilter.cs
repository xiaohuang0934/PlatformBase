using Microsoft.AspNetCore.Mvc.Filters;
using PlatformBase.Application.Services.SystemParamModule;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Services;

namespace PlatformBase.Host.Filters;

/// <summary>
/// 数据权限 ActionFilter — 根据用户所属部门物化路径限制数据可见范围
///
/// 实现逻辑：
///   1. 检查 Action 是否标注了 [DataScope] Attribute → 无标记则跳过
///   2. 平台管理员 → 不过滤，可查看所有数据
///   3. 租户管理员 → 查看全租户数据（含 null）
///   4. 普通用户 → 根据 org_null_data_visibility 参数配置 + AccessibleOrgPaths 过滤
///
/// 过滤条件注入：
///   - 将 DataScopeFilterContext 注入到 HttpContext.Items["DataScopeContext"]
///   - Service/Repository 层可读取并应用过滤条件
/// </summary>
public class DataScopeFilter : IAsyncActionFilter
{
    private readonly ICurrentUserContext _currentUser;
    private readonly ISystemParamService _sysParam;

    /// <summary>
    /// 构造函数：注入用户上下文和系统参数服务
    /// </summary>
    public DataScopeFilter(ICurrentUserContext currentUser, ISystemParamService sysParam)
    {
        _currentUser = currentUser;
        _sysParam = sysParam;
    }

    /// <summary>
    /// Action 执行前的数据权限拦截
    /// </summary>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 查找 Action 上是否标注了 [DataScope] Attribute
        var attr = context.ActionDescriptor.EndpointMetadata
            .OfType<DataScopeAttribute>().FirstOrDefault();
        if (attr == null)
        {
            await next();
            return;
        }

        // 未认证用户跳过（由其他中间件处理）
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == null)
        {
            await next();
            return;
        }

        // 超级管理员绕过数据权限过滤
        if (_currentUser.IsSuperAdmin)
        {
            var superAdminContext = new DataScopeFilterContext
            {
                UserType = _currentUser.UserType,
                TenantId = _currentUser.CurrentTenantId,
                AccessibleOrgPaths = [],
                NullDataVisibility = "all",
                CanViewAllTenant = true,
                IsSuperAdmin = true
            };
            context.HttpContext.Items["DataScopeContext"] = superAdminContext;
            await next();
            return;
        }

        // 构建数据权限过滤上下文
        var dataScopeContext = await BuildDataScopeContextAsync();

        // 注入到 HttpContext.Items，供 Service/Repository 层使用
        context.HttpContext.Items["DataScopeContext"] = dataScopeContext;

        await next();
    }

    /// <summary>
    /// 构建数据权限过滤上下文
    /// </summary>
    private async Task<DataScopeFilterContext> BuildDataScopeContextAsync()
    {
        var userType = _currentUser.UserType;
        var tenantId = _currentUser.CurrentTenantId;
        var accessiblePaths = _currentUser.AccessibleOrgPaths;

        // 读取 org_null_data_visibility 参数配置
        // 默认值：all（租户内全部可见）
        var nullVisibility = await _sysParam.GetValueAsync(
            "org_null_data_visibility",
            "all",
            default);

        return new DataScopeFilterContext
        {
            UserType = userType,
            TenantId = tenantId,
            AccessibleOrgPaths = accessiblePaths,
            NullDataVisibility = nullVisibility,
            // 租户管理员可查看全租户数据
            CanViewAllTenant = userType == UserType.TenantAdmin || userType == UserType.PlatformAdmin
        };
    }
}

/// <summary>
/// 数据权限过滤上下文（注入到 HttpContext.Items）
/// </summary>
public class DataScopeFilterContext
{
    /// <summary>用户类型</summary>
    public UserType UserType { get; set; }

    /// <summary>当前租户 ID</summary>
    public Guid? TenantId { get; set; }

    /// <summary>可访问的部门物化路径列表（含子级）</summary>
    public IReadOnlyList<string> AccessibleOrgPaths { get; set; } = [];

    /// <summary>
    /// 未归属部门数据的可见性配置：
    /// - "all": 租户内全部可见
    /// - "admin_only": 仅管理员可见
    /// </summary>
    public string NullDataVisibility { get; set; } = "all";

    /// <summary>是否可查看全租户数据（平台管理员/租户管理员）</summary>
    public bool CanViewAllTenant { get; set; }

    /// <summary>是否超级管理员（绕过所有数据权限过滤）</summary>
    public bool IsSuperAdmin { get; set; }
}
