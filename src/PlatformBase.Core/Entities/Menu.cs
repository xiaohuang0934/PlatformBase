namespace PlatformBase.Core.Entities;

/// <summary>
/// 菜单实体，支持目录/页面/按钮三级类型，树形结构
/// 通过 PermissionCode 绑定到 RBAC 权限点，前端根据 isGranted 渲染
/// </summary>
public class Menu : TenantSoftDeleteEntity
{
    /// <summary>菜单类型：1=目录 2=页面 3=按钮</summary>
    public int Type { get; set; } = 2;

    /// <summary>显示名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>父级菜单 ID（null=根节点）</summary>
    public Guid? ParentId { get; set; }

    /// <summary>前端路由路径（Type=2 时有值）</summary>
    public string? Path { get; set; }

    /// <summary>前端组件路径（Type=2 时有值）</summary>
    public string? Component { get; set; }

    /// <summary>图标 class</summary>
    public string? Icon { get; set; }

    /// <summary>绑定的权限编码（如 users.list）</summary>
    public string? PermissionCode { get; set; }

    /// <summary>排序</summary>
    public int SortOrder { get; set; }

    /// <summary>侧边栏是否可见</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>页面缓存（Vue keep-alive）</summary>
    public bool KeepAlive { get; set; }
}
