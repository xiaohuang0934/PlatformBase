using PlatformBase.Application.Services;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Repositories;
using PlatformBase.Core.Services;

namespace PlatformBase.Host.Services.MenuModule;

/// <summary>
/// 菜单服务实现
/// </summary>
public class MenuService : IMenuService
{
    private readonly IUnitOfWork _uow;
    private readonly IPermissionService _permService;
    private readonly ICurrentUserService _currentUser;

    public MenuService(IUnitOfWork uow, IPermissionService permService, ICurrentUserService currentUser)
    {
        _uow = uow;
        _permService = permService;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MenuNode>> GetUserMenuTreeAsync(CancellationToken ct = default)
    {
        var allMenus = await _uow.Repository<Menu>()
            .FindAsync(m => m.IsEnabled, ct);

        // 平台管理员看全部，不裁剪
        if (_currentUser.IsSuperAdmin)
            return BuildTree(allMenus, null); // 平台管理员看全部

        // 普通用户：取权限编码集合
        var userPerms = _currentUser.UserId != null
            ? await _permService.GetUserPermissionCodesAsync(_currentUser.UserId.Value, ct)
            : [];

        var grantedSet = new HashSet<string>(userPerms, StringComparer.OrdinalIgnoreCase);
        return BuildTree(allMenus, grantedSet);
    }

    public async Task<IReadOnlyList<Menu>> GetAllAsync(Guid? parentId = null, CancellationToken ct = default)
    {
        var repo = _uow.Repository<Menu>();
        if (parentId.HasValue)
            return await repo.FindAsync(m => m.IsEnabled && m.ParentId == parentId.Value, ct);
        // parentId 未传时，返回全部启用菜单（供桌面端构建树用）
        return await repo.FindAsync(m => m.IsEnabled, ct);
    }

    public async Task<Menu?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _uow.Repository<Menu>().GetByIdAsync(id, ct);

    public async Task<Menu> CreateAsync(Menu menu, CancellationToken ct = default)
    {
        var created = await _uow.Repository<Menu>().AddAsync(menu, ct);
        await _uow.SaveChangesAsync(ct);
        return created;
    }

    public async Task<Menu> UpdateAsync(Menu menu, CancellationToken ct = default)
    {
        _uow.Repository<Menu>().Update(menu);
        await _uow.SaveChangesAsync(ct);
        return menu;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Menu>().GetByIdAsync(id, ct);
        if (entity == null) return;
        _uow.Repository<Menu>().SoftDelete(entity);
        await _uow.SaveChangesAsync(ct);
    }

    private static List<MenuNode> BuildTree(List<Menu> all, HashSet<string>? grantedCodes)
    {
        var nodes = all.Select(m => new MenuNode
        {
            Id = m.Id, Name = m.Name, Type = m.Type, Path = m.Path, Component = m.Component,
            Icon = m.Icon, PermissionCode = m.PermissionCode, SortOrder = m.SortOrder,
            IsVisible = m.IsVisible, KeepAlive = m.KeepAlive
        }).ToDictionary(n => n.Id);

        var isGranted = all.ToDictionary(m => m.Id, m =>
            grantedCodes == null  // null = 平台管理员，看全部
            || string.IsNullOrEmpty(m.PermissionCode)
            || grantedCodes.Contains(m.PermissionCode));

        var roots = new List<MenuNode>();
        foreach (var m in all.OrderBy(m => m.SortOrder))
        {
            if (!isGranted[m.Id]) continue;
            var node = nodes[m.Id];
            if (m.ParentId != null && nodes.TryGetValue(m.ParentId.Value, out var parent) && isGranted[m.ParentId.Value])
                parent.Children.Add(node);
            else
                roots.Add(node);
        }
        return roots;
    }
}
