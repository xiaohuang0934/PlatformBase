using System.ComponentModel.DataAnnotations;

namespace PlatformBase.Application.Dtos.MenuModule;

/// <summary>
/// 创建菜单请求
/// </summary>
public class CreateMenuDto
{
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public int Type { get; set; } = 2;
    public Guid? ParentId { get; set; }
    public string? Path { get; set; }
    public string? Component { get; set; }
    public string? Icon { get; set; }
    public string? PermissionCode { get; set; }
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool KeepAlive { get; set; }
}
