namespace PlatformBase.Core.Entities;

/// <summary>
/// 数据字典项实体，字典类型下的具体枚举值（如"男"/"女"）
/// 支持层级结构（ParentId 自引用），可用于级联选择器等场景
/// 继承 <see cref="SoftDeleteEntity"/> 获得审计追踪 + 软删除能力
/// </summary>
public class DataDictItem : SoftDeleteEntity
{
    /// <summary>所属字典类型 ID</summary>
    public Guid DictTypeId { get; set; }

    /// <summary>项编码（如 male / female），同一类型下唯一</summary>
    public string ItemCode { get; set; } = string.Empty;

    /// <summary>项显示名称（如 "男" / "女"）</summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>可选扩展值</summary>
    public string? ItemValue { get; set; }

    /// <summary>父级项 ID，NULL 表示顶层</summary>
    public Guid? ParentId { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>同类内排序</summary>
    public int SortOrder { get; set; }

    /// <summary>所属字典类型导航属性</summary>
    public DataDictType DictType { get; set; } = null!;

    /// <summary>父级项导航属性</summary>
    public DataDictItem? Parent { get; set; }

    /// <summary>子项集合导航属性</summary>
    public ICollection<DataDictItem> Children { get; set; } = [];
}
