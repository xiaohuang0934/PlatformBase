using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlatformBase.Application.Dtos;
using PlatformBase.Application.Services;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Extensions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Repositories;
using PlatformBase.Infrastructure.Data;
using StackExchange.Redis;

namespace PlatformBase.Host.Services.DataDictModule;

/// <summary>
/// 数据字典服务实现
/// 快查接口优先从 Redis 缓存读取，缓存未命中时查询数据库并回写
/// 变更操作自动失效对应类型的缓存
/// Redis 不可用时自动降级到数据库，零额外开销
/// </summary>
public class DataDictService : IDataDictService
{
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _context;
    private readonly IDatabase? _redis;

    private const int CacheExpirationMinutes = 30;
    private const string CacheKeyPrefix = "dict:";

    public DataDictService(IUnitOfWork uow, AppDbContext context, IServiceProvider serviceProvider)
    {
        _uow = uow;
        _context = context;
        _redis = serviceProvider.GetService<IConnectionMultiplexer>()?.GetDatabase();
    }

    // ═══════════════════ 字典类型管理 ═══════════════════

    public async Task<PagedResult<DataDictTypeDto>> GetTypesAsync(
        DataDictTypeQuery query, CancellationToken ct = default)
    {
        Expression<Func<DataDictType, bool>>? filter = null;
        if (query.IsEnabled.HasValue)
        {
            var enabled = query.IsEnabled.Value;
            filter = t => t.IsEnabled == enabled;
        }

        var kw = query.Keyword?.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(kw))
        {
            filter = filter.Append(t =>
                (t.TypeName != null && t.TypeName.ToUpper().Contains(kw))
                || (t.TypeCode != null && t.TypeCode.ToUpper().Contains(kw)));
        }

        var request = new PagedRequest
        {
            PageIndex = query.PageIndex,
            PageSize = query.PageSize,
            SortField = query.SortField ?? nameof(DataDictType.SortOrder),
            IsAscending = query.IsAscending,
        };

        var result = await _uow.Repository<DataDictType>().GetPagedAsync(request, filter, ct);
        return new PagedResult<DataDictTypeDto>(
            result.TotalCount, result.PageIndex, result.PageSize,
            result.Items.Select(TypeToDto));
    }

    public async Task<DataDictTypeDto?> GetTypeByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<DataDictType>().GetByIdAsync(id, ct);
        return entity == null ? null : TypeToDto(entity);
    }

    public async Task<DataDictTypeDto> CreateTypeAsync(CreateDataDictTypeDto dto, CancellationToken ct = default)
    {
        var exists = await _uow.Repository<DataDictType>()
            .AnyAsync(t => t.TypeCode == dto.TypeCode, ct);
        if (exists)
            throw new BusinessException($"字典类型编码 '{dto.TypeCode}' 已存在", ErrorCode.DuplicateRecord);

        var entity = new DataDictType
        {
            TypeCode = dto.TypeCode,
            TypeName = dto.TypeName,
            Description = dto.Description,
            SortOrder = dto.SortOrder,
            IsEnabled = true
        };
        var created = await _uow.Repository<DataDictType>().AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return TypeToDto(created);
    }

    public async Task<DataDictTypeDto> UpdateTypeAsync(Guid id, UpdateDataDictTypeDto dto,
        CancellationToken ct = default)
    {
        var entity = await _uow.Repository<DataDictType>().GetByIdAsync(id, ct);
        if (entity == null)
            throw new BusinessException("字典类型不存在", ErrorCode.DataNotFound);

        if (dto.TypeName != null) entity.TypeName = dto.TypeName;
        if (dto.Description != null) entity.Description = dto.Description;
        if (dto.IsEnabled.HasValue) entity.IsEnabled = dto.IsEnabled.Value;
        if (dto.SortOrder.HasValue) entity.SortOrder = dto.SortOrder.Value;

        _uow.Repository<DataDictType>().Update(entity);
        await _uow.SaveChangesAsync(ct);
        await InvalidateCacheAsync(entity.TypeCode);
        return TypeToDto(entity);
    }

    /// <summary>删除字典类型，级联软删除所有关联项</summary>
    public async Task DeleteTypeAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<DataDictType>().GetByIdAsync(id, ct);
        if (entity == null) return;

        var items = await _context.Set<DataDictItem>()
            .Where(i => i.DictTypeId == id)
            .ToListAsync(ct);

        foreach (var item in items)
            _uow.Repository<DataDictItem>().SoftDelete(item);

        _uow.Repository<DataDictType>().SoftDelete(entity);
        await _uow.SaveChangesAsync(ct);
        await InvalidateCacheAsync(entity.TypeCode);
    }

    // ═══════════════════ 字典项管理 ═══════════════════

    public async Task<IReadOnlyList<DataDictItemDto>> GetItemsAsync(Guid dictTypeId,
        CancellationToken ct = default)
    {
        var items = await _context.Set<DataDictItem>()
            .Where(i => i.DictTypeId == dictTypeId)
            .OrderBy(i => i.SortOrder)
            .ToListAsync(ct);

        return BuildTree(items).ToList();
    }

    public async Task<DataDictItemDto?> GetItemByIdAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _context.Set<DataDictItem>()
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        return item == null ? null : ItemToDto(item);
    }

    public async Task<DataDictItemDto> CreateItemAsync(CreateDataDictItemDto dto,
        CancellationToken ct = default)
    {
        var exists = await _context.Set<DataDictItem>()
            .AnyAsync(i => i.DictTypeId == dto.DictTypeId && i.ItemCode == dto.ItemCode, ct);
        if (exists)
            throw new BusinessException($"字典项编码 '{dto.ItemCode}' 已存在", ErrorCode.DuplicateRecord);

        var item = new DataDictItem
        {
            DictTypeId = dto.DictTypeId,
            ItemCode = dto.ItemCode,
            ItemName = dto.ItemName,
            ItemValue = dto.ItemValue,
            ParentId = dto.ParentId,
            SortOrder = dto.SortOrder,
            IsEnabled = true
        };

        _context.Set<DataDictItem>().Add(item);
        await _uow.SaveChangesAsync(ct);

        var typeCode = await GetTypeCodeAsync(dto.DictTypeId, ct);
        await InvalidateCacheAsync(typeCode);
        return ItemToDto(item);
    }

    public async Task<DataDictItemDto> UpdateItemAsync(Guid id, UpdateDataDictItemDto dto,
        CancellationToken ct = default)
    {
        var item = await _context.Set<DataDictItem>()
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        if (item == null)
            throw new BusinessException("字典项不存在", ErrorCode.DataNotFound);

        if (dto.ItemName != null) item.ItemName = dto.ItemName;
        if (dto.ItemValue != null) item.ItemValue = dto.ItemValue;
        if (dto.ParentId.HasValue) item.ParentId = dto.ParentId.Value;
        if (dto.IsEnabled.HasValue) item.IsEnabled = dto.IsEnabled.Value;
        if (dto.SortOrder.HasValue) item.SortOrder = dto.SortOrder.Value;

        _context.Set<DataDictItem>().Update(item);
        await _uow.SaveChangesAsync(ct);

        var typeCode = await GetTypeCodeAsync(item.DictTypeId, ct);
        await InvalidateCacheAsync(typeCode);
        return ItemToDto(item);
    }

    public async Task DeleteItemAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _context.Set<DataDictItem>()
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        if (item == null) return;

        _uow.Repository<DataDictItem>().SoftDelete(item);
        await _uow.SaveChangesAsync(ct);

        var typeCode = await GetTypeCodeAsync(item.DictTypeId, ct);
        await InvalidateCacheAsync(typeCode);
    }

    // ═══════════════════ 快查接口（带缓存） ═══════════════════

    public async Task<IReadOnlyList<DataDictItemDto>> GetItemsByTypeCodeAsync(
        string typeCode, CancellationToken ct = default)
    {
        var cached = await TryGetCacheAsync(typeCode);
        if (cached != null) return cached;

        var type = await _uow.Repository<DataDictType>()
            .FirstOrDefaultAsync(t => t.TypeCode == typeCode && t.IsEnabled, ct);
        if (type == null) return [];

        var items = await _context.Set<DataDictItem>()
            .Where(i => i.DictTypeId == type.Id && i.IsEnabled)
            .OrderBy(i => i.SortOrder)
            .ToListAsync(ct);

        var result = BuildTree(items).ToList();
        await TrySetCacheAsync(typeCode, result);
        return result;
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<DataDictItemDto>>> GetItemsByTypeCodesAsync(
        IReadOnlyList<string> typeCodes, CancellationToken ct = default)
    {
        var result = new Dictionary<string, IReadOnlyList<DataDictItemDto>>();
        foreach (var code in typeCodes)
            result[code] = await GetItemsByTypeCodeAsync(code, ct);
        return result;
    }

    // ═══════════════════ 缓存操作 ═══════════════════

    private async Task InvalidateCacheAsync(string typeCode)
    {
        if (_redis == null) return;
        try { await _redis.KeyDeleteAsync($"{CacheKeyPrefix}{typeCode}"); }
        catch { /* Redis 不可用，降级跳过 */ }
    }

    private async Task<IReadOnlyList<DataDictItemDto>?> TryGetCacheAsync(string typeCode)
    {
        if (_redis == null) return null;
        try
        {
            var value = await _redis.StringGetAsync($"{CacheKeyPrefix}{typeCode}");
            if (value.HasValue && !value.IsNullOrEmpty)
                return JsonSerializer.Deserialize<List<DataDictItemDto>>(value!);
        }
        catch { /* Redis 不可用，降级跳过 */ }
        return null;
    }

    private async Task TrySetCacheAsync(string typeCode, IReadOnlyList<DataDictItemDto> items)
    {
        if (_redis == null) return;
        try
        {
            await _redis.StringSetAsync(
                $"{CacheKeyPrefix}{typeCode}",
                JsonSerializer.Serialize(items),
                TimeSpan.FromMinutes(CacheExpirationMinutes));
        }
        catch { /* Redis 不可用，降级跳过 */ }
    }

    private async Task<string> GetTypeCodeAsync(Guid dictTypeId, CancellationToken ct)
    {
        var type = await _uow.Repository<DataDictType>().GetByIdAsync(dictTypeId, ct);
        return type?.TypeCode ?? string.Empty;
    }

    // ═══════════════════ 树形组装 ═══════════════════

    /// <summary>将平铺项列表组装为树形结构</summary>
    private static IReadOnlyList<DataDictItemDto> BuildTree(List<DataDictItem> allItems)
    {
        var dtos = allItems.Select(ItemToDto).ToDictionary(d => d.Id);

        foreach (var item in allItems)
        {
            if (item.ParentId.HasValue && dtos.TryGetValue(item.ParentId.Value, out var parentDto))
            {
                if (dtos.TryGetValue(item.Id, out var childDto))
                    parentDto.Children.Add(childDto);
            }
        }

        return dtos.Values.Where(d => !d.ParentId.HasValue)
            .OrderBy(d => d.SortOrder).ToList();
    }

    private static DataDictTypeDto TypeToDto(DataDictType entity) => new()
    {
        Id = entity.Id,
        TypeCode = entity.TypeCode,
        TypeName = entity.TypeName,
        Description = entity.Description,
        IsEnabled = entity.IsEnabled,
        SortOrder = entity.SortOrder,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    private static DataDictItemDto ItemToDto(DataDictItem entity) => new()
    {
        Id = entity.Id,
        DictTypeId = entity.DictTypeId,
        ItemCode = entity.ItemCode,
        ItemName = entity.ItemName,
        ItemValue = entity.ItemValue,
        ParentId = entity.ParentId,
        IsEnabled = entity.IsEnabled,
        SortOrder = entity.SortOrder,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}
