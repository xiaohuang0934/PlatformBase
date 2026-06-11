using Microsoft.EntityFrameworkCore;
using PlatformBase.Application.Services;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Repositories;
using PlatformBase.Infrastructure.Data;

namespace PlatformBase.Host.Services.TenantModule;

/// <summary>
/// 租户管理服务实现
/// </summary>
public class TenantService : ITenantService
{
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _context;

    public TenantService(IUnitOfWork uow, AppDbContext context) { _uow = uow; _context = context; }

    public async Task<IReadOnlyList<Tenant>> GetPagedAsync(int pageIndex, int pageSize, CancellationToken ct = default)
    {
        var result = await _uow.Repository<Tenant>()
            .GetPagedAsync(new Core.Models.PagedRequest { PageIndex = pageIndex, PageSize = pageSize }, null, ct);
        return result.Items.ToList();
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
        => await _uow.Repository<Tenant>().CountAsync(null, ct);

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _uow.Repository<Tenant>().GetByIdAsync(id, ct);

    public async Task<Tenant> CreateAsync(string name, string code, string? email, CancellationToken ct = default)
    {
        if (await _uow.Repository<Tenant>().AnyAsync(t => t.Code == code, ct))
            throw new BusinessException("租户编码已存在", ErrorCode.DuplicateRecord);
        var entity = new Tenant { Name = name, Code = code, ContactEmail = email };
        var created = await _uow.Repository<Tenant>().AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return created;
    }

    public async Task UpdateAsync(Guid id, string? name, string? email, CancellationToken ct = default)
    {
        var t = await GetByIdAsync(id, ct) ?? throw new BusinessException("租户不存在", ErrorCode.DataNotFound);
        if (name != null) t.Name = name;
        if (email != null) t.ContactEmail = email;
        _uow.Repository<Tenant>().Update(t);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DisableAsync(Guid id, CancellationToken ct = default)
    {
        var t = await GetByIdAsync(id, ct) ?? throw new BusinessException("租户不存在", ErrorCode.DataNotFound);
        t.IsEnabled = false;
        _uow.Repository<Tenant>().Update(t);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetTenantIdsForPlatformUserAsync(Guid platformUserId, CancellationToken ct = default)
    {
        return await _context.Set<PlatformUserTenant>()
            .Where(p => p.PlatformUserId == platformUserId)
            .Select(p => p.TenantId).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetPlatformUserIdsForTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _context.Set<PlatformUserTenant>()
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.PlatformUserId).ToListAsync(ct);
    }

    public async Task AssignTenantToPlatformUserAsync(Guid platformUserId, Guid tenantId, CancellationToken ct = default)
    {
        var exists = await _context.Set<PlatformUserTenant>()
            .AnyAsync(p => p.PlatformUserId == platformUserId && p.TenantId == tenantId, ct);
        if (exists) return;
        _context.Set<PlatformUserTenant>().Add(new PlatformUserTenant { PlatformUserId = platformUserId, TenantId = tenantId });
        await _context.SaveChangesAsync(ct);
    }

    public async Task RemoveTenantFromPlatformUserAsync(Guid platformUserId, Guid tenantId, CancellationToken ct = default)
    {
        var entry = await _context.Set<PlatformUserTenant>()
            .FirstOrDefaultAsync(p => p.PlatformUserId == platformUserId && p.TenantId == tenantId, ct);
        if (entry != null)
        {
            _context.Set<PlatformUserTenant>().Remove(entry);
            await _context.SaveChangesAsync(ct);
        }
    }
}
