namespace PlatformBase.Application.Dtos;

/// <summary>
/// 更新权限请求
/// </summary>
public class UpdatePermissionDto
{
    public string? Name { get; set; }
    public string? ResourcePath { get; set; }
    public string? HttpMethod { get; set; }
    public string? GroupName { get; set; }
    public string? Description { get; set; }
    public bool? IsEnabled { get; set; }
    public int? SortOrder { get; set; }
}
