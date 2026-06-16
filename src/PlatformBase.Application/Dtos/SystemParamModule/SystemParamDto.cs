namespace PlatformBase.Application.Dtos.SystemParamModule;

/// <summary>
/// 系统参数列表/详情 DTO
/// </summary>
public class SystemParamDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public bool Inheritable { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
