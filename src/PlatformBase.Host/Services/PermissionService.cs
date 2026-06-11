using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlatformBase.Application.Dtos;
using PlatformBase.Application.Services;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Repositories;
using PlatformBase.Core.Extensions;
using PlatformBase.Infrastructure.Data;
using StackExchange.Redis;

namespace PlatformBase.Host.Services;

/// <summary>
/// 权限查询服务实现
/// 优先从 Redis 缓存读取用户权限编码列表，缓存未命中时查询数据库并回写
/// Redis 不可用或 IConnectionMultiplexer 为 null 时直接查库，零额外开销
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _context;
    private readonly IDatabase? _redis;

    private const int CacheExpirationMinutes = 5; // 缩短 TTL 以降低权限变更后的不一致窗口
    private const string CacheKeyPrefix = "user:perms:";

    public PermissionService(IUnitOfWork uow, AppDbContext context, IServiceProvider serviceProvider)
    {
        _uow = uow;
        _context = context;
        _redis = serviceProvider.GetService<IConnectionMultiplexer>()?.GetDatabase();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetUserPermissionCodesAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var cached = await TryGetCacheAsync(userId);
        if (cached != null)
            return cached;

        var codes = await QueryPermissionCodesFromDbAsync(userId, cancellationToken);
        await TrySetCacheAsync(userId, codes);
        return codes;
    }

    /// <inheritdoc />
    public async Task<bool> HasPermissionAsync(Guid userId, string permissionCode,
        CancellationToken cancellationToken = default)
    {
        var codes = await GetUserPermissionCodesAsync(userId, cancellationToken);
        return codes.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task InvalidateUserCacheAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_redis == null) return;
        try { await _redis.KeyDeleteAsync($"{CacheKeyPrefix}{userId}"); }
        catch { /* Redis 不可用，降级跳过 */ }
    }

    // ═══════════════════ 权限点 CRUD 管理（v1.1） ═══════════════════

    public async Task<PagedResult<PermissionDto>> GetPagedAsync(
        PermissionQuery query, CancellationToken ct = default)
    {
        var kw = query.Keyword?.Trim().ToUpperInvariant();
        var group = query.GroupName?.Trim();

        var filter = ((Expression<Func<Permission, bool>>?)null)
            .AppendIf(!string.IsNullOrWhiteSpace(query.ResourcePath),
                p => p.ResourcePath == query.ResourcePath!.Trim())
            .AppendIf(!string.IsNullOrWhiteSpace(group),
                p => p.GroupName == group)
            .AppendIf(query.IsEnabled.HasValue,
                p => p.IsEnabled == query.IsEnabled.Value)
            .AppendIf(!string.IsNullOrWhiteSpace(kw),
                p => p.Code.ToUpper().Contains(kw!) || p.Name.ToUpper().Contains(kw!));

        var result = await _uow.Repository<Permission>().GetPagedAsync(new PagedRequest
        {
            PageIndex = query.PageIndex, PageSize = query.PageSize,
            SortField = query.SortField ?? nameof(Permission.SortOrder), IsAscending = query.IsAscending
        }, filter, ct);
        return new PagedResult<PermissionDto>(
            result.TotalCount, result.PageIndex, result.PageSize,
            result.Items.Select(ToDto));
    }

    public async Task<PermissionDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Permission>().GetByIdAsync(id, ct);
        return entity == null ? null : ToDto(entity);
    }

    public async Task<PermissionDto> CreateAsync(CreatePermissionDto dto, CancellationToken ct = default)
    {
        var exists = await _uow.Repository<Permission>()
            .AnyAsync(p => p.Code == dto.Code, ct);
        if (exists)
            throw new BusinessException($"权限编码 '{dto.Code}' 已存在", ErrorCode.DuplicateRecord);

        var entity = new Permission
        {
            Code = dto.Code,
            Name = dto.Name,
            ResourcePath = dto.ResourcePath,
            HttpMethod = dto.HttpMethod,
            GroupName = dto.GroupName,
            Description = dto.Description,
            SortOrder = dto.SortOrder,
            IsEnabled = true
        };

        var created = await _uow.Repository<Permission>().AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(created);
    }

    public async Task<PermissionDto> UpdateAsync(Guid id, UpdatePermissionDto dto, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Permission>().GetByIdAsync(id, ct);
        if (entity == null)
            throw new BusinessException("权限不存在", ErrorCode.DataNotFound);

        if (dto.Name != null) entity.Name = dto.Name;
        if (dto.ResourcePath != null) entity.ResourcePath = dto.ResourcePath;
        if (dto.HttpMethod != null) entity.HttpMethod = dto.HttpMethod;
        if (dto.GroupName != null) entity.GroupName = dto.GroupName;
        if (dto.Description != null) entity.Description = dto.Description;
        if (dto.IsEnabled.HasValue) entity.IsEnabled = dto.IsEnabled.Value;
        if (dto.SortOrder.HasValue) entity.SortOrder = dto.SortOrder.Value;

        _uow.Repository<Permission>().Update(entity);
        await _uow.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Permission>().GetByIdAsync(id, ct);
        if (entity == null) return;

        // 级联清理角色-权限关联
        var rolePerms = await _context.Set<RolePermission>()
            .Where(rp => rp.PermissionId == id).ToListAsync(ct);
        _context.Set<RolePermission>().RemoveRange(rolePerms);

        // 级联清理用户-权限关联
        var userPerms = await _context.Set<UserPermission>()
            .Where(up => up.PermissionId == id).ToListAsync(ct);

        // 失效受影响用户的权限缓存
        foreach (var up in userPerms)
            await InvalidateUserCacheAsync(up.UserId, ct);

        _context.Set<UserPermission>().RemoveRange(userPerms);
        _uow.Repository<Permission>().Delete(entity);
        await _uow.SaveChangesAsync(ct);
    }

    private static PermissionDto ToDto(Permission entity) => new()
    {
        Id = entity.Id,
        Code = entity.Code,
        Name = entity.Name,
        ResourcePath = entity.ResourcePath,
        HttpMethod = entity.HttpMethod,
        GroupName = entity.GroupName,
        SortOrder = entity.SortOrder,
        IsEnabled = entity.IsEnabled,
        Description = entity.Description
    };

    private async Task<IReadOnlyList<string>> QueryPermissionCodesFromDbAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        HashSet<string> grantedCodes = [];

        var userRoleIds = await _context.Set<UserRole>()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);

        if (userRoleIds.Count != 0)
        {
            var rolePermIds = await _context.Set<RolePermission>()
                .Where(rp => userRoleIds.Contains(rp.RoleId))
                .Select(rp => rp.PermissionId)
                .ToListAsync(cancellationToken);

            if (rolePermIds.Count != 0)
            {
                var permissions = await _uow.Repository<Permission>()
                    .FindAsync(p => rolePermIds.Contains(p.Id) && p.IsEnabled, cancellationToken);
                foreach (var p in permissions)
                    grantedCodes.Add(p.Code);
            }
        }

        var userPermissions = await _context.Set<UserPermission>()
            .Where(up => up.UserId == userId)
            .ToListAsync(cancellationToken);

        if (userPermissions.Count != 0)
        {
            var userPermIds = userPermissions.Select(up => up.PermissionId).ToList();
            var permissions = await _uow.Repository<Permission>()
                .FindAsync(p => userPermIds.Contains(p.Id), cancellationToken);

            foreach (var up in userPermissions)
            {
                var perm = permissions.FirstOrDefault(p => p.Id == up.PermissionId);
                if (perm == null || !perm.IsEnabled) continue;

                if (up.IsGranted)
                    grantedCodes.Add(perm.Code);
                else
                    grantedCodes.Remove(perm.Code);
            }
        }

        return grantedCodes.ToList();
    }

    private async Task<IReadOnlyList<string>?> TryGetCacheAsync(Guid userId)
    {
        if (_redis == null) return null;
        try
        {
            var value = await _redis.StringGetAsync($"{CacheKeyPrefix}{userId}");
            if (value.HasValue)
                return JsonSerializer.Deserialize<List<string>>(value!) ?? [];
        }
        catch { /* Redis 不可用，降级跳过 */ }
        return null;
    }

    private async Task TrySetCacheAsync(Guid userId, IReadOnlyList<string> codes)
    {
        if (_redis == null) return;
        try
        {
            await _redis.StringSetAsync(
                $"{CacheKeyPrefix}{userId}",
                JsonSerializer.Serialize(codes),
                TimeSpan.FromMinutes(CacheExpirationMinutes));
        }
        catch { /* Redis 不可用，降级跳过 */ }
    }
}
