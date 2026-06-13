using PlatformBase.Core.Entities;

namespace PlatformBase.Application.Services.MenuModule;

/// <summary>
/// 菜单服务接口
/// </summary>
public interface IMenuService
{
    /// <summary>获取当前用户可访问的菜单树（已裁剪无权限节点）</summary>
    Task<IReadOnlyList<MenuNode>> GetUserMenuTreeAsync(CancellationToken ct = default);

    /// <summary>查询全部菜单，支持按父级ID筛选（parentId=null 返回一级菜单，parentId有值返回该父级下的子菜单）</summary>
    Task<IReadOnlyList<Menu>> GetAllAsync(Guid? parentId = null, CancellationToken ct = default);

    /// <summary>根据 ID 查询</summary>
    Task<Menu?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>创建</summary>
    Task<Menu> CreateAsync(Menu menu, CancellationToken ct = default);

    /// <summary>更新</summary>
    Task<Menu> UpdateAsync(Menu menu, CancellationToken ct = default);

    /// <summary>删除（软删除）</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// 菜单树节点（返回给前端）
/// </summary>
public class MenuNode
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public string? Path { get; set; }
    public string? Component { get; set; }
    public string? Icon { get; set; }
    public string? PermissionCode { get; set; }
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; }
    public bool KeepAlive { get; set; }
    public List<MenuNode> Children { get; set; } = [];
}
