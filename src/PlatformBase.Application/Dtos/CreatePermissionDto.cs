namespace PlatformBase.Application.Dtos;

/// <summary>
/// 创建权限请求
/// </summary>
public class CreatePermissionDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ResourcePath { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string? GroupName { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}
