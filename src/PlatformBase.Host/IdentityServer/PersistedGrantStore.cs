using IdentityServer4.Stores;
using Microsoft.EntityFrameworkCore;
using PlatformBase.Core.Entities;
using PlatformBase.Infrastructure.Data;

namespace PlatformBase.Host.IdentityServer;

/// <summary>
/// IdentityServer4 IPersistedGrantStore 的 EF Core 实现（方案 B）
/// 将 refresh_token / reference_token 持久化到数据库
/// 服务重启不丢失，密码变更时可按用户批量撤销
/// </summary>
public class PersistedGrantStore : IPersistedGrantStore
{
    private readonly AppDbContext _context;

    public PersistedGrantStore(AppDbContext context)
    {
        _context = context;
    }

    public async Task StoreAsync(IdentityServer4.Models.PersistedGrant grant)
    {
        var existing = await _context.Set<PersistedGrantEntity>()
            .FirstOrDefaultAsync(g => g.Key == grant.Key);

        if (existing == null)
        {
            _context.Set<PersistedGrantEntity>().Add(MapToEntity(grant));
        }
        else
        {
            existing.SubjectId = grant.SubjectId;
            existing.SessionId = grant.SessionId;
            existing.ClientId = grant.ClientId;
            existing.Description = grant.Description;
            existing.CreationTime = grant.CreationTime;
            existing.Expiration = grant.Expiration;
            existing.ConsumedTime = grant.ConsumedTime;
            existing.Data = grant.Data;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<IdentityServer4.Models.PersistedGrant?> GetAsync(string key)
    {
        var entity = await _context.Set<PersistedGrantEntity>()
            .FirstOrDefaultAsync(g => g.Key == key);

        return entity == null ? null : MapToModel(entity);
    }

    public async Task<IEnumerable<IdentityServer4.Models.PersistedGrant>> GetAllAsync(
        IdentityServer4.Stores.PersistedGrantFilter filter)
    {
        var query = _context.Set<PersistedGrantEntity>().AsQueryable();

        if (!string.IsNullOrEmpty(filter.SubjectId))
            query = query.Where(g => g.SubjectId == filter.SubjectId);
        if (!string.IsNullOrEmpty(filter.ClientId))
            query = query.Where(g => g.ClientId == filter.ClientId);
        if (!string.IsNullOrEmpty(filter.SessionId))
            query = query.Where(g => g.SessionId == filter.SessionId);
        if (!string.IsNullOrEmpty(filter.Type))
            query = query.Where(g => g.Type == filter.Type);

        var entities = await query.ToListAsync();
        return entities.Select(MapToModel);
    }

    public async Task RemoveAsync(string key)
    {
        var entity = await _context.Set<PersistedGrantEntity>()
            .FirstOrDefaultAsync(g => g.Key == key);

        if (entity != null)
        {
            _context.Set<PersistedGrantEntity>().Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task RemoveAllAsync(IdentityServer4.Stores.PersistedGrantFilter filter)
    {
        var grants = await GetAllAsync(filter);

        foreach (var grant in grants)
        {
            var entity = await _context.Set<PersistedGrantEntity>()
                .FirstOrDefaultAsync(g => g.Key == grant.Key);
            if (entity != null)
                _context.Set<PersistedGrantEntity>().Remove(entity);
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// 撤销指定用户的所有授权（密码变更时调用）
    /// </summary>
    public async Task RevokeUserTokensAsync(string userId)
    {
        await RemoveAllAsync(new IdentityServer4.Stores.PersistedGrantFilter { SubjectId = userId });
    }

    private static PersistedGrantEntity MapToEntity(IdentityServer4.Models.PersistedGrant grant)
    {
        return new PersistedGrantEntity
        {
            Key = grant.Key,
            Type = grant.Type,
            SubjectId = grant.SubjectId,
            SessionId = grant.SessionId,
            ClientId = grant.ClientId,
            Description = grant.Description,
            CreationTime = grant.CreationTime,
            Expiration = grant.Expiration,
            ConsumedTime = grant.ConsumedTime,
            Data = grant.Data
        };
    }

    private static IdentityServer4.Models.PersistedGrant MapToModel(PersistedGrantEntity entity)
    {
        return new IdentityServer4.Models.PersistedGrant
        {
            Key = entity.Key,
            Type = entity.Type,
            SubjectId = entity.SubjectId,
            SessionId = entity.SessionId,
            ClientId = entity.ClientId,
            Description = entity.Description,
            CreationTime = entity.CreationTime,
            Expiration = entity.Expiration,
            ConsumedTime = entity.ConsumedTime,
            Data = entity.Data
        };
    }
}
