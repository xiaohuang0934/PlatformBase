using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PlatformBase.Application.Dtos;
using PlatformBase.Application.Services;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Repositories;
using PlatformBase.Infrastructure.Data;

namespace PlatformBase.Host.Services;

/// <summary>
/// 角色管理服务实现，提供角色的完整 CRUD + 权限分配能力
/// </summary>
public class RoleService : IRoleService
{
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _context;

    public RoleService(IUnitOfWork uow, AppDbContext context)
    {
        _uow = uow;
        _context = context;
    }

    public async Task<PagedResult<RoleDto>> GetPagedAsync(RoleQuery query, CancellationToken ct = default)
    {
        Expression<Func<Role, bool>>? filter = null;
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim().ToUpperInvariant();
            filter = r => r.NormalizedName.Contains(kw);
        }

        var request = new PagedRequest
        {
            PageIndex = query.PageIndex,
            PageSize = query.PageSize,
            SortField = query.SortField ?? nameof(Role.Name),
            IsAscending = query.IsAscending
        };

        var result = await _uow.Repository<Role>().GetPagedAsync(request, filter, ct);
        return new PagedResult<RoleDto>(
            result.TotalCount, result.PageIndex, result.PageSize,
            result.Items.Select(ToDto));
    }

    public async Task<RoleDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Role>().GetByIdAsync(id, ct);
        return entity == null ? null : ToDto(entity);
    }

    public async Task<RoleDto> CreateAsync(CreateRoleDto dto, CancellationToken ct = default)
    {
        var normalized = Normalize(dto.Name);
        var exists = await _uow.Repository<Role>()
            .AnyAsync(r => r.NormalizedName == normalized, ct);
        if (exists)
            throw new BusinessException($"角色名称 '{dto.Name}' 已存在", ErrorCode.DuplicateRecord);

        var role = new Role
        {
            Name = dto.Name,
            NormalizedName = normalized,
            Description = dto.Description
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
            var normalized = Normalize(dto.Name);
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
    }

    private static RoleDto ToDto(Role entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    private static string Normalize(string value) => (value ?? string.Empty).ToUpperInvariant();
}
