using Microsoft.EntityFrameworkCore;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Repositories;
using PlatformBase.Core.Services;
using PlatformBase.Infrastructure.Data;

namespace PlatformBase.Host.Services.MenuModule;

/// <summary>
/// 菜单服务实现
/// 支持按用户类型过滤菜单，平台管理员看全部，租户用户需有菜单关联
/// </summary>
public class MenuService : IMenuService
{
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _context;
    private readonly IPermissionService _permService;
    private readonly ICurrentUserContext _currentUser;

    // 租户用户不可见的菜单权限编码前缀
    private static readonly HashSet<string> TenantUserBannedPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        "tenants.",       // 租户管理
        "system-params.", // 系统参数
        "jobs.",          // 定时任务
        "operation-logs." // 操作日志
    };

    public MenuService(IUnitOfWork uow, AppDbContext context, IPermissionService permService, ICurrentUserContext currentUser)
    {
        _uow = uow;
        _context = context;
        _permService = permService;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MenuNode>> GetUserMenuTreeAsync(CancellationToken ct = default)
    {
        var allMenus = await _uow.Repository<Menu>()
            .FindAsync(m => m.IsEnabled, ct);

        // 平台管理员看全部，不裁剪
        if (_currentUser.UserType == UserType.PlatformAdmin)
            return BuildTree(allMenus, null, null);

        // 获取用户权限编码集合
        var userPerms = _currentUser.UserId != null
            ? await _permService.GetUserPermissionCodesAsync(_currentUser.UserId.Value, ct)
            : [];

        // 租户管理员：过滤掉租户不可见的菜单
        if (_currentUser.UserType == UserType.TenantAdmin)
        {
            var filteredMenus = allMenus
                .Where(m => !IsBannedForTenantUser(m.PermissionCode))
                .ToList();
            return BuildTree(filteredMenus, new HashSet<string>(userPerms, StringComparer.OrdinalIgnoreCase), null);
        }

        // 租户普通用户：需要权限匹配 + 菜单关联
        var grantedSet = new HashSet<string>(userPerms, StringComparer.OrdinalIgnoreCase);
        var userMenuIds = _currentUser.UserId != null
            ? await GetUserMenuIdsAsync(_currentUser.UserId.Value, ct)
            : [];

        // 过滤：权限匹配 且 有菜单关联（或菜单无权限编码要求）
        var visibleMenus = allMenus
            .Where(m => !IsBannedForTenantUser(m.PermissionCode))
            .Where(m => string.IsNullOrEmpty(m.PermissionCode) || grantedSet.Contains(m.PermissionCode))
            .Where(m => userMenuIds.Count == 0 || userMenuIds.Contains(m.Id))
            .ToList();

        return BuildTree(visibleMenus, grantedSet, userMenuIds);
    }

    public async Task<IReadOnlyList<Menu>> GetAllAsync(Guid? parentId = null, CancellationToken ct = default)
    {
        var repo = _uow.Repository<Menu>();
        if (parentId.HasValue)
            return await repo.FindAsync(m => m.IsEnabled && m.ParentId == parentId.Value, ct);
        return await repo.FindAsync(m => m.IsEnabled, ct);
    }

    public async Task<Menu?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _uow.Repository<Menu>().GetByIdAsync(id, ct);

    public async Task<Menu> CreateAsync(Menu menu, CancellationToken ct = default)
    {
        if (menu.ParentId != null)
        {
            var parent = await _uow.Repository<Menu>().GetByIdAsync(menu.ParentId.Value, ct);
            if (parent == null)
                throw new BusinessException("父菜单不存在", ErrorCode.DataNotFound);

            if (_currentUser.TenantId != null && parent.TenantId != _currentUser.TenantId)
                throw new BusinessException("父菜单不属于当前租户", ErrorCode.ParentOrgNotInTenant);
        }

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

    public async Task AssignMenusToUserAsync(Guid userId, IReadOnlyList<Guid> menuIds, CancellationToken ct = default)
    {
        var user = await _uow.Repository<User>().GetByIdAsync(userId, ct);
        if (user == null)
            throw new BusinessException("用户不存在", ErrorCode.DataNotFound);

        // 租户管理员只能给本租户用户分配菜单
        if (_currentUser.UserType == UserType.TenantAdmin)
        {
            if (user.TenantId != _currentUser.TenantId)
                throw new BusinessException("只能给本租户用户分配菜单", ErrorCode.Forbidden);
        }

        // 验证菜单是否存在
        if (menuIds.Count > 0)
        {
            var existingMenus = await _uow.Repository<Menu>()
                .FindAsync(m => menuIds.Contains(m.Id), ct);
            if (existingMenus.Count != menuIds.Count)
                throw new BusinessException("部分菜单不存在", ErrorCode.DataNotFound);
        }

        // 删除旧的关联
        var existingRelations = await _context.Set<UserMenu>()
            .Where(um => um.UserId == userId)
            .ToListAsync(ct);
        _context.Set<UserMenu>().RemoveRange(existingRelations);

        // 添加新的关联
        foreach (var menuId in menuIds)
        {
            _context.Set<UserMenu>().Add(new UserMenu
            {
                UserId = userId,
                MenuId = menuId
            });
        }

        await _uow.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetUserMenuIdsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.Set<UserMenu>()
            .AsNoTracking()
            .Where(um => um.UserId == userId)
            .Select(um => um.MenuId)
            .ToListAsync(ct);
    }

    /// <summary>
    /// 判断菜单是否对租户用户禁止
    /// </summary>
    private static bool IsBannedForTenantUser(string? permissionCode)
    {
        if (string.IsNullOrEmpty(permissionCode))
            return false;

        return TenantUserBannedPermissions.Any(banned => permissionCode.StartsWith(banned, StringComparison.OrdinalIgnoreCase));
    }

    private static List<MenuNode> BuildTree(List<Menu> all, HashSet<string>? grantedCodes, IReadOnlyList<Guid>? userMenuIds)
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

            // 如果有用户菜单限制，检查是否在列表中
            if (userMenuIds != null && userMenuIds.Count > 0 && !userMenuIds.Contains(m.Id))
                continue;

            var node = nodes[m.Id];
            if (m.ParentId != null && nodes.TryGetValue(m.ParentId.Value, out var parent) && isGranted[m.ParentId.Value])
                parent.Children.Add(node);
            else
                roots.Add(node);
        }

        // 过滤掉没有子菜单的父级目录（Type=1）
        return roots.Where(n => n.Type != 1 || n.Children.Count > 0).ToList();
    }
}
