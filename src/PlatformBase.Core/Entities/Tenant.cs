namespace PlatformBase.Core.Entities;

/// <summary>
/// 租户实体
/// </summary>
public class Tenant : AuditableEntity
{
    /// <summary>租户名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>租户编码（唯一）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>联系人邮箱</summary>
    public string? ContactEmail { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;
}
