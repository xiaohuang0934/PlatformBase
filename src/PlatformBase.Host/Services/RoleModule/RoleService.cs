using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Extensions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Repositories;
using PlatformBase.Core.Services;
using PlatformBase.Infrastructure.Data;
using StackExchange.Redis;
using Role = PlatformBase.Core.Entities.Role;

namespace PlatformBase.Host.Services.RoleModule;

/// <summary>
/// 角色管理服务实现，提供角色的完整 CRUD + 权限分配能力
/// 权限变更时自动失效受影响用户的权限缓存
/// </summary>
public class RoleService : IRoleService
{
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _context;
    private readonly IDatabase? _redis;
    private readonly ICurrentUserContext _currentUser;
    private readonly IDataScopeAuthorizationService _authService;

    public RoleService(IUnitOfWork uow, AppDbContext context, IServiceProvider serviceProvider,
        ICurrentUserContext currentUser, IDataScopeAuthorizationService authService)
    {
        _uow = uow;
        _context = context;
        _redis = serviceProvider.GetService<IConnectionMultiplexer>()?.GetDatabase();
        _currentUser = currentUser;
        _authService = authService;
    }

    public async Task<PagedResult<RoleDto>> GetPagedAsync(RoleQuery query, CancellationToken ct = default)
    {
        var kw = query.Keyword?.Trim().ToUpperInvariant();

        // 计算生效的租户列表
        IReadOnlyList<Guid> effectiveTenantIds = ResolveEffectiveTenantIds(query.TenantIds);

        var filter = ((Expression<Func<Role, bool>>?)null)
            .AppendIf(effectiveTenantIds.Count > 0 && _currentUser.UserType == UserType.PlatformAdmin,
                r => r.TenantId == null || effectiveTenantIds.Contains(r.TenantId.Value))
            .AppendIf(effectiveTenantIds.Count > 0 && _currentUser.UserType != UserType.PlatformAdmin,
                r => r.TenantId == effectiveTenantIds.FirstOrDefault())
            .AppendIf(effectiveTenantIds.Count == 0 && _currentUser.UserType != UserType.PlatformAdmin,
                r => false)
            .AppendIf(effectiveTenantIds.Count == 0 && _currentUser.UserType == UserType.PlatformAdmin,
                r => r.TenantId == null)
            .AppendIf(!string.IsNullOrWhiteSpace(kw), r => r.NormalizedName.Contains(kw!))
            .AppendIf(query.IsSystem.HasValue, r => r.IsSystem == query.IsSystem.Value);

        var result = await _uow.Repository<Role>().GetPagedAsync(new PagedRequest
        {
            PageIndex = query.PageIndex, PageSize = query.PageSize,
            SortField = query.SortField ?? nameof(Role.Name), IsAscending = query.IsAscending
        }, filter, ct);
        return new PagedResult<RoleDto>(
            result.TotalCount, result.PageIndex, result.PageSize,
            result.Items.Select(ToDto));
    }

    /// <summary>
    /// 解析生效的租户列表：
    /// - 平台用户传了 tenantIds → 校验后返回
    /// - 否则 → 返回 CurrentTenantIds
    /// </summary>
    private IReadOnlyList<Guid> ResolveEffectiveTenantIds(List<Guid>? queryTenantIds)
    {
        if (_currentUser.UserType != UserType.PlatformAdmin)
            return _currentUser.CurrentTenantIds;

        if (queryTenantIds == null || queryTenantIds.Count == 0)
            return _currentUser.CurrentTenantIds;

        var invalid = queryTenantIds.Where(t => !_currentUser.TenantIds.Contains(t)).ToList();
        if (invalid.Count > 0)
            throw new BusinessException($"无权访问租户: {invalid[0]}", ErrorCode.Forbidden);

        return queryTenantIds;
    }

    public async Task<RoleDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Role>().GetByIdAsync(id, ct);
        return entity == null ? null : ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<List<Role>> GetByIdsAsync(List<Guid> ids, CancellationToken ct = default)
    {
        if (ids == null || ids.Count == 0) return [];
        return await _uow.Repository<Role>()
            .FindAsync(r => ids.Contains(r.Id), ct);
    }

    public async Task<RoleDto> CreateAsync(CreateRoleDto dto, CancellationToken ct = default)
    {
        var normalized = StringExtensions.Normalize(dto.Name);
        var exists = await _uow.Repository<Role>()
            .AnyAsync(r => r.NormalizedName == normalized, ct);
        if (exists)
            throw new BusinessException($"角色名称 '{dto.Name}' 已存在", ErrorCode.DuplicateRecord);

        // 集中式权限校验 + 租户覆盖
        var auth = await _authService.AuthorizeAsync(
            bannedUserTypes: [UserType.TenantUser],
            requestTenantId: dto.TenantId,
            requestUserType: null,
            requestOrgIds: null,
            requestRoleIds: null,
            cancellationToken: ct);

        if (auth.IsBanned)
            throw new BusinessException(auth.BanReason ?? "无权操作", ErrorCode.NoPermissionToOperate);

        var role = new Role
        {
            Name = dto.Name,
            Code = dto.Code,
            NormalizedName = normalized,
            Description = dto.Description,
            TenantId = auth.TenantId
        };

        var created = await _uow.Repository<Role>().AddAsync(role, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(created);
    }

    public async Task<RoleDto> UpdateAsync(Guid id, UpdateRoleDto dto, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Role>().GetByIdAsync(id, ct);
        if (entity == null)
            throw new BusinessException("角色不存在", ErrorCode.DataNotFound);

        if (dto.Name != null)
        {
            var normalized = StringExtensions.Normalize(dto.Name);
            var exists = await _uow.Repository<Role>()
                .AnyAsync(r => r.NormalizedName == normalized && r.Id != id, ct);
            if (exists)
                throw new BusinessException($"角色名称 '{dto.Name}' 已存在", ErrorCode.DuplicateRecord);

            entity.Name = dto.Name;
            entity.NormalizedName = normalized;
        }

        if (dto.Description != null)
            entity.Description = dto.Description;

        _uow.Repository<Role>().Update(entity);
        await _uow.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Role>().GetByIdAsync(id, ct);
        if (entity == null) return;

        // 租户管理员只能删除本租户的角色
        if (_currentUser.UserType == UserType.TenantAdmin)
        {
            if (entity.TenantId != null && entity.TenantId != _currentUser.TenantId)
                throw new BusinessException("只能删除本租户的角色", ErrorCode.Forbidden);
        }

        var hasUsers = await _context.Set<UserRole>()
            .AnyAsync(ur => ur.RoleId == id, ct);
        if (hasUsers)
            throw new BusinessException("该角色下尚有用户关联，请先移除用户后再删除角色", ErrorCode.InvalidOperation);

        // 级联清理角色-权限关联
        var rolePerms = await _context.Set<RolePermission>()
            .Where(rp => rp.RoleId == id).ToListAsync(ct);
        _context.Set<RolePermission>().RemoveRange(rolePerms);

        _uow.Repository<Role>().Delete(entity);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetPermissionCodesAsync(Guid roleId, CancellationToken ct = default)
    {
        var permIds = await _context.Set<RolePermission>()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .ToListAsync(ct);

        if (permIds.Count == 0) return [];

        var permissions = await _uow.Repository<Permission>()
            .FindAsync(p => permIds.Contains(p.Id) && p.IsEnabled, ct);

        return permissions.Select(p => p.Code).ToList();
    }

    public async Task AssignPermissionsAsync(Guid roleId, IReadOnlyList<string> permissionCodes,
        CancellationToken ct = default)
    {
        var role = await _uow.Repository<Role>().GetByIdAsync(roleId, ct);
        if (role == null)
            throw new BusinessException("角色不存在", ErrorCode.DataNotFound);

        // 租户管理员只能编辑本租户的角色
        if (_currentUser.UserType == UserType.TenantAdmin)
        {
            if (role.TenantId != null && role.TenantId != _currentUser.TenantId)
                throw new BusinessException("只能编辑本租户的角色", ErrorCode.Forbidden);
        }

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var existing = await _context.Set<RolePermission>()
                .Where(rp => rp.RoleId == roleId)
                .ToListAsync(ct);
            _context.Set<RolePermission>().RemoveRange(existing);

            if (permissionCodes.Count > 0)
            {
                var permissions = await _uow.Repository<Permission>()
                    .FindAsync(p => permissionCodes.Contains(p.Code), ct);

                foreach (var perm in permissions)
                {
                    _context.Set<RolePermission>().Add(new RolePermission
                    {
                        RoleId = roleId,
                        PermissionId = perm.Id
                    });
                }
            }

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }

        await InvalidateAffectedUsersCacheAsync(roleId, ct);
    }

    private static RoleDto ToDto(Role entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Code = entity.Code,
        Description = entity.Description,
        IsSystem = entity.IsSystem,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    /// <summary>失效所有拥有指定角色的用户权限缓存</summary>
    private async Task InvalidateAffectedUsersCacheAsync(Guid roleId, CancellationToken ct)
    {
        if (_redis == null) return;
        try
        {
            var userIds = await _context.Set<UserRole>()
                .Where(ur => ur.RoleId == roleId)
                .Select(ur => ur.UserId)
                .ToListAsync(ct);

            foreach (var userId in userIds)
                await _redis.KeyDeleteAsync($"user:perms:{userId}");
        }
        catch { /* Redis 不可用，降级跳过 */ }
    }
}
