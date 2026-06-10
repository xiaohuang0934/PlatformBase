namespace PlatformBase.Core.Entities;

/// <summary>
/// 数据字典类型实体，定义一组字典项的集合（如"性别"、"用户状态"等）
/// 继承 <see cref="SoftDeleteEntity"/> 获得审计追踪 + 软删除能力
/// </summary>
public class DataDictType : SoftDeleteEntity
{
    /// <summary>字典类型编码，全局唯一（如 gender / user_status）</summary>
    public string TypeCode { get; set; } = string.Empty;

    /// <summary>字典类型名称（如 "性别" / "用户状态"）</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>类型说明</summary>
    public string? Description { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>类型排序</summary>
    public int SortOrder { get; set; }

    /// <summary>字典项导航属性</summary>
    public ICollection<DataDictItem> Items { get; set; } = [];
}
