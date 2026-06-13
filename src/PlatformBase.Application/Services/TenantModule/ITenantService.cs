using PlatformBase.Core.Entities;
using PlatformBase.Core.Models;

namespace PlatformBase.Application.Services.TenantModule;

/// <summary>
/// 租户管理服务接口，提供租户 CRUD 和平台账号-租户关联管理
/// </summary>
public interface ITenantService
{
    /// <summary>分页查询租户列表，支持 keyword(搜索Name/Code/ContactEmail)、isEnabled筛选</summary>
    Task<PagedResult<Tenant>> GetPagedAsync(string? keyword = null, bool? isEnabled = null,
        int pageIndex = 1, int pageSize = 10, string? sortField = null, bool isAscending = true,
        CancellationToken ct = default);

    /// <summary>根据ID查询租户</summary>
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>创建租户</summary>
    Task<Tenant> CreateAsync(string name, string code, string? email, CancellationToken ct = default);
    /// <summary>更新租户名称/邮箱</summary>
    Task UpdateAsync(Guid id, string? name, string? email, CancellationToken ct = default);
    /// <summary>停用租户（IsEnabled=false）</summary>
    Task DisableAsync(Guid id, CancellationToken ct = default);

    // ───── 平台账号-租户关联 ─────
    /// <summary>查询平台账号已分配的租户ID列表</summary>
    Task<IReadOnlyList<Guid>> GetTenantIdsForPlatformUserAsync(Guid platformUserId, CancellationToken ct = default);
    /// <summary>查询租户下已分配的平台账号ID列表</summary>
    Task<IReadOnlyList<Guid>> GetPlatformUserIdsForTenantAsync(Guid tenantId, CancellationToken ct = default);
    /// <summary>将平台账号分配到指定租户</summary>
    Task AssignTenantToPlatformUserAsync(Guid platformUserId, Guid tenantId, CancellationToken ct = default);
    /// <summary>移除平台账号的租户关联</summary>
    Task RemoveTenantFromPlatformUserAsync(Guid platformUserId, Guid tenantId, CancellationToken ct = default);
}
