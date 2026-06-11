namespace PlatformBase.Application.Dtos.DataDictModule;

/// <summary>
/// 更新数据字典类型请求
/// </summary>
public class UpdateDataDictTypeDto
{
    public string? TypeName { get; set; }
    public string? Description { get; set; }
    public bool? IsEnabled { get; set; }
    public int? SortOrder { get; set; }
}
