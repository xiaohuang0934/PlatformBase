namespace PlatformBase.Application.Dtos.MenuModule;

/// <summary>
/// 更新菜单请求
/// </summary>
public class UpdateMenuDto
{
    public string? Name { get; set; }
    public int? Type { get; set; }
    public Guid? ParentId { get; set; }
    public string? Path { get; set; }
    public string? Component { get; set; }
    public string? Icon { get; set; }
    public string? PermissionCode { get; set; }
    public int? SortOrder { get; set; }
    public bool? IsVisible { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? KeepAlive { get; set; }
}
