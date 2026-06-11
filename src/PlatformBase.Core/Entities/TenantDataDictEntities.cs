namespace PlatformBase.Core.Entities;

/// <summary>
/// 租户覆盖字典类型（双表方案，不继承租户基类因自身即租户数据）
/// </summary>
public class TenantDataDictType : TenantSoftDeleteEntity
{
    public string TypeCode { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
}

/// <summary>
/// 租户覆盖字典项（双表方案）
/// </summary>
public class TenantDataDictItem : TenantSoftDeleteEntity
{
    public Guid DictTypeId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? ItemValue { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
}
