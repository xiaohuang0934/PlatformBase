using PlatformBase.Application.Dtos;
using PlatformBase.Core.Models;

namespace PlatformBase.Application.Services;

/// <summary>
/// 数据字典服务接口，提供字典类型/项的 CRUD 管理及高性能快查接口
/// 快查接口带 Redis 缓存，30 分钟过期，Redis 不可用时自动降级到数据库
/// </summary>
public interface IDataDictService
{
    // ───── 字典类型管理 ─────
    Task<PagedResult<DataDictTypeDto>> GetTypesAsync(DataDictTypeQuery query, CancellationToken ct = default);
    Task<DataDictTypeDto?> GetTypeByIdAsync(Guid id, CancellationToken ct = default);
    Task<DataDictTypeDto> CreateTypeAsync(CreateDataDictTypeDto dto, CancellationToken ct = default);
    Task<DataDictTypeDto> UpdateTypeAsync(Guid id, UpdateDataDictTypeDto dto, CancellationToken ct = default);
    Task DeleteTypeAsync(Guid id, CancellationToken ct = default);

    // ───── 字典项管理 ─────
    Task<IReadOnlyList<DataDictItemDto>> GetItemsAsync(Guid dictTypeId, CancellationToken ct = default);
    Task<DataDictItemDto?> GetItemByIdAsync(Guid id, CancellationToken ct = default);
    Task<DataDictItemDto> CreateItemAsync(CreateDataDictItemDto dto, CancellationToken ct = default);
    Task<DataDictItemDto> UpdateItemAsync(Guid id, UpdateDataDictItemDto dto, CancellationToken ct = default);
    Task DeleteItemAsync(Guid id, CancellationToken ct = default);

    // ───── 快查接口（前端/业务模块高性能入口）─────
    /// <summary>按类型编码获取所有启用项（带 Redis 缓存，自动组装树形结构）</summary>
    Task<IReadOnlyList<DataDictItemDto>> GetItemsByTypeCodeAsync(string typeCode, CancellationToken ct = default);

    /// <summary>批量获取多组字典，减少网络往返</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<DataDictItemDto>>> GetItemsByTypeCodesAsync(
        IReadOnlyList<string> typeCodes, CancellationToken ct = default);
}
