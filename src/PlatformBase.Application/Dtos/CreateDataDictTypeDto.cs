namespace PlatformBase.Application.Dtos;

/// <summary>
/// 创建数据字典类型请求
/// </summary>
public class CreateDataDictTypeDto
{
    public string TypeCode { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}
