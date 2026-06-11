namespace PlatformBase.Application.Dtos.DataDictModule;

/// <summary>
/// 数据字典类型 DTO
/// </summary>
public class DataDictTypeDto
{
    public Guid Id { get; set; }
    public string TypeCode { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
