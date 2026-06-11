namespace PlatformBase.Application.Dtos.DataDictModule;

/// <summary>
/// 更新数据字典项请求
/// </summary>
public class UpdateDataDictItemDto
{
    public string? ItemName { get; set; }
    public string? ItemValue { get; set; }
    public Guid? ParentId { get; set; }
    public bool? IsEnabled { get; set; }
    public int? SortOrder { get; set; }
}
