using PlatformBase.Core.Models;

namespace PlatformBase.Application.Services.DataDictModule;

/// <summary>
/// 数据字典服务接口，提供字典类型/项的 CRUD 管理及高性能快查接口
/// 快查接口带 Redis 缓存，30 分钟过期，Redis 不可用时自动降级到数据库
/// </summary>
public interface IDataDictService
{
    // ───── 字典类型管理 ─────
    /// <summary>分页查询字典类型</summary>
    Task<PagedResult<DataDictTypeDto>> GetTypesAsync(DataDictTypeQuery query, CancellationToken ct = default);
    /// <summary>根据ID查询类型</summary>
    Task<DataDictTypeDto?> GetTypeByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>创建字典类型</summary>
    Task<DataDictTypeDto> CreateTypeAsync(CreateDataDictTypeDto dto, CancellationToken ct = default);
    /// <summary>更新字典类型（变更时自动失效缓存）</summary>
    Task<DataDictTypeDto> UpdateTypeAsync(Guid id, UpdateDataDictTypeDto dto, CancellationToken ct = default);
    /// <summary>删除字典类型（级联软删除所有项）</summary>
    Task DeleteTypeAsync(Guid id, CancellationToken ct = default);

    // ───── 字典项管理 ─────
    /// <summary>获取类型下所有项（树形结构）</summary>
    Task<IReadOnlyList<DataDictItemDto>> GetItemsAsync(Guid dictTypeId, CancellationToken ct = default);
    /// <summary>根据ID查询项</summary>
    Task<DataDictItemDto?> GetItemByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>创建字典项（变更时自动失效缓存）</summary>
    Task<DataDictItemDto> CreateItemAsync(CreateDataDictItemDto dto, CancellationToken ct = default);
    /// <summary>更新字典项</summary>
    Task<DataDictItemDto> UpdateItemAsync(Guid id, UpdateDataDictItemDto dto, CancellationToken ct = default);
    /// <summary>删除字典项（软删除）</summary>
    Task DeleteItemAsync(Guid id, CancellationToken ct = default);

    // ───── 快查接口 ─────
    /// <summary>按类型编码获取所有启用项（Redis缓存+树形组装）</summary>
    Task<IReadOnlyList<DataDictItemDto>> GetItemsByTypeCodeAsync(string typeCode, CancellationToken ct = default);
    /// <summary>批量获取多组字典项</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<DataDictItemDto>>> GetItemsByTypeCodesAsync(
        IReadOnlyList<string> typeCodes, CancellationToken ct = default);
}
