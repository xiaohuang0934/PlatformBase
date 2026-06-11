namespace PlatformBase.Core.Entities;

/// <summary>
/// 租户覆盖参数实体（双表方案）
/// </summary>
public class TenantParam : TenantSoftDeleteEntity
{
    /// <summary>参数编码（同一租户下唯一）</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
}
