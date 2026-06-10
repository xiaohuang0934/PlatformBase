namespace PlatformBase.Application.Dtos;

/// <summary>
/// 创建数据字典项请求
/// </summary>
public class CreateDataDictItemDto
{
    public Guid DictTypeId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? ItemValue { get; set; }
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
}
