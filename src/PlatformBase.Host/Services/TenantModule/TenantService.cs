using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Extensions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Repositories;
using PlatformBase.Infrastructure.Data;
using StackExchange.Redis;

namespace PlatformBase.Host.Services.TenantModule;

/// <summary>
/// 租户管理服务实现
/// </summary>
public class TenantService : ITenantService
{
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _context;
    private readonly IDatabase? _redis;

    private const string TenantCacheKeyPrefix = "user:tenant:";

    public TenantService(IUnitOfWork uow, AppDbContext context, IServiceProvider serviceProvider)
    {
        _uow = uow;
        _context = context;
        _redis = serviceProvider.GetService<IConnectionMultiplexer>()?.GetDatabase();
    }

    public async Task<PagedResult<Tenant>> GetPagedAsync(
        string? keyword = null, bool? isEnabled = null,
        int pageIndex = 1, int pageSize = 10, string? sortField = null, bool isAscending = true,
        CancellationToken ct = default)
    {
        var kw = keyword?.Trim().ToUpperInvariant();
        Expression<Func<Tenant, bool>>? filter = null;

        if (!string.IsNullOrWhiteSpace(kw))
        {
            filter = filter.Append(t =>
                (t.Name != null && t.Name.ToUpper().Contains(kw))
                || (t.Code != null && t.Code.ToUpper().Contains(kw))
                || (t.ContactEmail != null && t.ContactEmail.ToUpper().Contains(kw)));
        }

        if (isEnabled.HasValue)
        {
            filter = filter.AppendIf(true, t => t.IsEnabled == isEnabled.Value);
        }

        return await _uow.Repository<Tenant>().GetPagedAsync(new PagedRequest
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            SortField = sortField ?? nameof(Tenant.Name),
            IsAscending = isAscending,
        }, filter, ct);
    }

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _uow.Repository<Tenant>().GetByIdAsync(id, ct);

    public async Task<Tenant> CreateAsync(string name, string code, string? email, CancellationToken ct = default)
    {
        var tenant = new Tenant { Name = name, Code = code, ContactEmail = email };
        var created = await _uow.Repository<Tenant>().AddAsync(tenant, ct);
        await _uow.SaveChangesAsync(ct);
        return created;
    }

    public async Task UpdateAsync(Guid id, string? name, string? email, CancellationToken ct = default)
    {
        var tenant = await _uow.Repository<Tenant>().GetByIdAsync(id, ct);
        if (tenant == null) throw new BusinessException("租户不存在", ErrorCode.DataNotFound);
        if (name != null) tenant.Name = name;
        if (email != null) tenant.ContactEmail = email;
        _uow.Repository<Tenant>().Update(tenant);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DisableAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await _uow.Repository<Tenant>().GetByIdAsync(id, ct);
        if (tenant == null) throw new BusinessException("租户不存在", ErrorCode.DataNotFound);
        tenant.IsEnabled = false;
        _uow.Repository<Tenant>().Update(tenant);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetTenantIdsForPlatformUserAsync(
        Guid platformUserId, CancellationToken ct = default)
    {
        return await _context.PlatformUserTenants
            .Where(p => p.PlatformUserId == platformUserId)
            .Select(p => p.TenantId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetPlatformUserIdsForTenantAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        return await _context.PlatformUserTenants
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.PlatformUserId)
            .ToListAsync(ct);
    }

    public async Task AssignTenantToPlatformUserAsync(
        Guid platformUserId, Guid tenantId, CancellationToken ct = default)
    {
        var exists = await _context.PlatformUserTenants
            .AnyAsync(p => p.PlatformUserId == platformUserId && p.TenantId == tenantId, ct);
        if (exists) return;
        _context.PlatformUserTenants.Add(new PlatformUserTenant
        { PlatformUserId = platformUserId, TenantId = tenantId });
        await _uow.SaveChangesAsync(ct);

        // 失效平台用户的租户缓存
        InvalidateTenantCache(platformUserId);
    }

    public async Task RemoveTenantFromPlatformUserAsync(
        Guid platformUserId, Guid tenantId, CancellationToken ct = default)
    {
        var entity = await _context.PlatformUserTenants
            .FirstOrDefaultAsync(p => p.PlatformUserId == platformUserId && p.TenantId == tenantId, ct);
        if (entity != null)
        {
            _context.PlatformUserTenants.Remove(entity);
            await _uow.SaveChangesAsync(ct);

            // 失效平台用户的租户缓存
            InvalidateTenantCache(platformUserId);
        }
    }

    /// <summary>
    /// 失效指定用户的租户缓存（Redis DEL user:tenant:{userId}）
    /// </summary>
    private void InvalidateTenantCache(Guid userId)
    {
        if (_redis == null) return;
        try { _redis.KeyDelete($"{TenantCacheKeyPrefix}{userId}"); }
        catch { }
    }
}
