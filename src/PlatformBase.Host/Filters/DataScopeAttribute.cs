namespace PlatformBase.Host.Filters;

/// <summary>
/// 数据权限标记特性，声明该接口需要按部门行级过滤
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class DataScopeAttribute : Attribute
{
    /// <summary>关联的业务类型，用于后续多类型扩展</summary>
    public string? BizType { get; set; }
}
