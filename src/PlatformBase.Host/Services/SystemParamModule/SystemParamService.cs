using System.ComponentModel;
using System.Linq.Expressions;
using System.Text.Json;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Extensions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Repositories;
using StackExchange.Redis;

namespace PlatformBase.Host.Services.SystemParamModule;

/// <summary>
/// 系统参数服务实现
/// 优先从 Redis 缓存读取参数值，缓存未命中时查询数据库并回写
/// Redis 不可用时自动降级到数据库，零额外开销
/// </summary>
public class SystemParamService : ISystemParamService
{
    private readonly IUnitOfWork _uow;
    private readonly IDatabase? _redis;

    private const int CacheExpirationMinutes = 30;
    private const string CacheKeyPrefix = "sysparam:";
    private const string CatCacheKeyPrefix = "sysparam:cat:";
    private const string FeatureToggleCategory = "feature-toggle";

    public SystemParamService(IUnitOfWork uow, IServiceProvider serviceProvider)
    {
        _uow = uow;
        _redis = serviceProvider.GetService<IConnectionMultiplexer>()?.GetDatabase();
    }

    /// <inheritdoc />
    public async Task<string?> GetValueAsync(string code, CancellationToken cancellationToken = default)
    {
        var cached = await TryGetCacheAsync(code);
        if (cached != null)
            return cached == "__NULL__" ? null : cached;

        var param = await _uow.Repository<SystemParam>()
            .FirstOrDefaultAsync(p => p.Code == code && p.IsEnabled, cancellationToken);

        await TrySetCacheAsync(code, param?.Value);
        return param?.Value;
    }

    /// <inheritdoc />
    public async Task<T> GetValueAsync<T>(string code, T defaultValue, CancellationToken cancellationToken = default)
    {
        var raw = await GetValueAsync(code, cancellationToken);
        if (raw == null) return defaultValue;

        try
        {
            return (T)TypeDescriptor.GetConverter(typeof(T)).ConvertFromInvariantString(raw)!;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetByCategoryAsync(
        string category, CancellationToken cancellationToken = default)
    {
        var cached = await TryGetCatCacheAsync(category);
        if (cached != null) return cached;

        var items = await _uow.Repository<SystemParam>()
            .FindAsync(p => p.Category == category && p.IsEnabled, cancellationToken);

        var dict = items.OrderBy(p => p.SortOrder)
            .ToDictionary(p => p.Code, p => p.Value);

        await TrySetCatCacheAsync(category, dict);
        return dict;
    }

    /// <inheritdoc />
    public async Task<bool> IsFeatureEnabledAsync(string featureCode, bool defaultValue = false,
        CancellationToken cancellationToken = default)
    {
        var param = await _uow.Repository<SystemParam>()
            .FirstOrDefaultAsync(p => p.Code == featureCode && p.Category == FeatureToggleCategory && p.IsEnabled,
                cancellationToken);

        if (param == null) return defaultValue;

        return string.Equals(param.Value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SystemParamDto>> GetAllFeaturesAsync(
        CancellationToken cancellationToken = default)
    {
        var items = await _uow.Repository<SystemParam>()
            .FindAsync(p => p.Category == FeatureToggleCategory && p.IsEnabled, cancellationToken);

        return items.OrderBy(p => p.SortOrder).Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<PagedResult<SystemParamDto>> GetPagedAsync(
        SystemParamQuery query, CancellationToken cancellationToken = default)
    {
        var kw = query.Keyword?.Trim().ToUpperInvariant();
        var cat = query.Category?.Trim();

        var filter = ((Expression<Func<SystemParam, bool>>?)null)
            .AppendIf(!string.IsNullOrWhiteSpace(cat), p => p.Category == cat)
            .AppendIf(!string.IsNullOrWhiteSpace(kw), p => p.Code.ToUpper().Contains(kw!) || p.Name.ToUpper().Contains(kw!))
            .AppendIf(query.IsEnabled.HasValue, p => p.IsEnabled == query.IsEnabled.Value);

        var result = await _uow.Repository<SystemParam>().GetPagedAsync(new PagedRequest
        {
            PageIndex = query.PageIndex, PageSize = query.PageSize,
            SortField = query.SortField ?? nameof(SystemParam.SortOrder), IsAscending = query.IsAscending
        }, filter, cancellationToken);

        return new PagedResult<SystemParamDto>(
            result.TotalCount, result.PageIndex, result.PageSize,
            result.Items.Select(ToDto));
    }

    /// <inheritdoc />
    public async Task<SystemParamDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.Repository<SystemParam>().GetByIdAsync(id, cancellationToken);
        return entity == null ? null : ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<SystemParamDto> CreateAsync(CreateSystemParamDto dto, CancellationToken cancellationToken = default)
    {
        var exists = await _uow.Repository<SystemParam>()
            .AnyAsync(p => p.Code == dto.Code, cancellationToken);
        if (exists)
            throw new BusinessException($"参数编码 '{dto.Code}' 已存在", ErrorCode.DuplicateRecord);

        ValidateFeatureToggleValue(dto.Category, dto.Value);

        var entity = new SystemParam
        {
            Code = dto.Code,
            Name = dto.Name,
            Value = dto.Value,
            Category = dto.Category,
            Description = dto.Description,
            SortOrder = dto.SortOrder,
            IsEnabled = true
        };

        var created = await _uow.Repository<SystemParam>().AddAsync(entity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        if (created.Category != null)
            await InvalidateCatCacheAsync(created.Category);

        return ToDto(created);
    }

    /// <inheritdoc />
    public async Task<SystemParamDto> UpdateAsync(Guid id, UpdateSystemParamDto dto,
        CancellationToken cancellationToken = default)
    {
        var entity = await _uow.Repository<SystemParam>().GetByIdAsync(id, cancellationToken);
        if (entity == null)
            throw new BusinessException("系统参数不存在", ErrorCode.DataNotFound);

        if (dto.Name != null) entity.Name = dto.Name;
        if (dto.Value != null)
        {
            var category = dto.Category ?? entity.Category;
            ValidateFeatureToggleValue(category, dto.Value);
            entity.Value = dto.Value;
        }
        if (dto.Category != null) entity.Category = dto.Category;
        if (dto.Description != null) entity.Description = dto.Description;
        if (dto.IsEnabled.HasValue) entity.IsEnabled = dto.IsEnabled.Value;
        if (dto.SortOrder.HasValue) entity.SortOrder = dto.SortOrder.Value;

        _uow.Repository<SystemParam>().Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);

        await InvalidateCacheAsync(entity.Code, cancellationToken);
        if (entity.Category != null)
            await InvalidateCatCacheAsync(entity.Category);

        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.Repository<SystemParam>().GetByIdAsync(id, cancellationToken);
        if (entity == null) return;

        _uow.Repository<SystemParam>().SoftDelete(entity);
        await _uow.SaveChangesAsync(cancellationToken);

        await InvalidateCacheAsync(entity.Code, cancellationToken);
        if (entity.Category != null)
            await InvalidateCatCacheAsync(entity.Category);
    }

    /// <inheritdoc />
    public async Task InvalidateCacheAsync(string code, CancellationToken cancellationToken = default)
    {
        if (_redis == null) return;
        try { await _redis.KeyDeleteAsync($"{CacheKeyPrefix}{code}"); }
        catch { /* Redis 不可用，降级跳过 */ }
    }

    private async Task InvalidateCatCacheAsync(string category)
    {
        if (_redis == null) return;
        try { await _redis.KeyDeleteAsync($"{CatCacheKeyPrefix}{category}"); }
        catch { /* Redis 不可用，降级跳过 */ }
    }

    private async Task<string?> TryGetCacheAsync(string code)
    {
        if (_redis == null) return null;
        try
        {
            var value = await _redis.StringGetAsync($"{CacheKeyPrefix}{code}");
            return value.HasValue ? value.ToString() : null;
        }
        catch { return null; }
    }

    private async Task TrySetCacheAsync(string code, string? value)
    {
        if (_redis == null) return;
        try
        {
            await _redis.StringSetAsync(
                $"{CacheKeyPrefix}{code}",
                value ?? "__NULL__",
                TimeSpan.FromMinutes(CacheExpirationMinutes));
        }
        catch { /* Redis 不可用，降级跳过 */ }
    }

    private async Task<IReadOnlyDictionary<string, string>?> TryGetCatCacheAsync(string category)
    {
        if (_redis == null) return null;
        try
        {
            var value = await _redis.StringGetAsync($"{CatCacheKeyPrefix}{category}");
            if (value.HasValue && !value.IsNullOrEmpty)
                return JsonSerializer.Deserialize<Dictionary<string, string>>(value!);
        }
        catch { /* Redis 不可用，降级跳过 */ }
        return null;
    }

    private async Task TrySetCatCacheAsync(string category, IReadOnlyDictionary<string, string> dict)
    {
        if (_redis == null) return;
        try
        {
            await _redis.StringSetAsync(
                $"{CatCacheKeyPrefix}{category}",
                JsonSerializer.Serialize(dict),
                TimeSpan.FromMinutes(CacheExpirationMinutes));
        }
        catch { /* Redis 不可用，降级跳过 */ }
    }

    /// <summary>校验功能开关的值必须为 true 或 false</summary>
    private static void ValidateFeatureToggleValue(string? category, string value)
    {
        if (string.Equals(category, FeatureToggleCategory, StringComparison.OrdinalIgnoreCase))
        {
            var normalized = value?.Trim().ToLowerInvariant();
            if (normalized != "true" && normalized != "false")
                throw new BusinessException("功能开关的值必须为 true 或 false");
        }
    }

    private static SystemParamDto ToDto(SystemParam entity) => new()
    {
        Id = entity.Id,
        Code = entity.Code,
        Name = entity.Name,
        Value = entity.Value,
        Category = entity.Category,
        Description = entity.Description,
        IsEnabled = entity.IsEnabled,
        SortOrder = entity.SortOrder,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}
