using PlatformBase.Core.Models;

namespace PlatformBase.Application.Services.SystemParamModule;

/// <summary>
/// 系统参数服务接口，提供参数值读取、CRUD 管理、功能开关判断等功能
/// 读取接口带 Redis 缓存，30 分钟过期，Redis 不可用时自动降级到数据库
/// </summary>
public interface ISystemParamService
{
    /// <summary>以原始 string 获取单个参数值（带 Redis 缓存）</summary>
    Task<string?> GetValueAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>泛型获取参数值，缓存未命中或不存在时返回默认值（自动类型转换）</summary>
    Task<T> GetValueAsync<T>(string code, T defaultValue, CancellationToken cancellationToken = default);

    /// <summary>获取指定分类下所有启用参数（带 Redis 缓存，返回 Code→Value 字典）</summary>
    Task<IReadOnlyDictionary<string, string>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>判断功能开关是否开启（仅限 Category="feature-toggle"）</summary>
    Task<bool> IsFeatureEnabledAsync(string featureCode, bool defaultValue = false, CancellationToken cancellationToken = default);

    /// <summary>获取所有功能开关列表</summary>
    Task<IReadOnlyList<SystemParamDto>> GetAllFeaturesAsync(CancellationToken cancellationToken = default);

    /// <summary>分页查询</summary>
    Task<PagedResult<SystemParamDto>> GetPagedAsync(SystemParamQuery query, CancellationToken cancellationToken = default);

    /// <summary>根据 ID 查询单条</summary>
    Task<SystemParamDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>创建系统参数</summary>
    Task<SystemParamDto> CreateAsync(CreateSystemParamDto dto, CancellationToken cancellationToken = default);

    /// <summary>更新系统参数</summary>
    Task<SystemParamDto> UpdateAsync(Guid id, UpdateSystemParamDto dto, CancellationToken cancellationToken = default);

    /// <summary>删除系统参数（软删除）</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>清除指定编码的缓存</summary>
    Task InvalidateCacheAsync(string code, CancellationToken cancellationToken = default);
}

