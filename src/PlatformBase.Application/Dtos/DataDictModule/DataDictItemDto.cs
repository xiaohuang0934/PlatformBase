namespace PlatformBase.Application.Dtos.DataDictModule;

/// <summary>
/// 数据字典项 DTO，含子项列表支持树形结构
/// </summary>
public class DataDictItemDto
{
    public Guid Id { get; set; }
    public Guid DictTypeId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? ItemValue { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsEnabled { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>子项列表（树形结构用）</summary>
    public List<DataDictItemDto> Children { get; set; } = [];
}
