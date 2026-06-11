using PlatformBase.Application.Dtos;
using PlatformBase.Core.Models;

namespace PlatformBase.Application.Services;

/// <summary>
/// 权限服务接口，负责权限鉴权查询 + 权限点的完整 CRUD 管理
/// 鉴权查询优先使用 Redis 缓存（5min TTL），缓存未命中时查询数据库并回写
/// </summary>
public interface IPermissionService
{
    /// <summary>获取指定用户的所有权限编码列表（合并角色权限 + 用户直达权限，含拒绝覆盖逻辑）</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限编码列表（如 ["users.list", "roles.create"]）</returns>
    Task<IReadOnlyList<string>> GetUserPermissionCodesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>判断用户是否拥有指定权限编码（大小写不敏感）</summary>
    Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken = default);

    /// <summary>清除指定用户的权限缓存（角色/权限变更时调用）</summary>
    Task InvalidateUserCacheAsync(Guid userId, CancellationToken cancellationToken = default);

    // ───── 权限点 CRUD 管理 ─────

    /// <summary>分页查询权限列表</summary>
    /// <param name="query">查询条件（分组/路径/启用状态/关键词）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果</returns>
    Task<PagedResult<PermissionDto>> GetPagedAsync(PermissionQuery query, CancellationToken cancellationToken = default);

    /// <summary>根据 ID 查询权限</summary>
    Task<PermissionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>创建权限点</summary>
    /// <param name="dto">创建请求（Code/Name/ResourcePath/HttpMethod）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的权限 DTO</returns>
    Task<PermissionDto> CreateAsync(CreatePermissionDto dto, CancellationToken cancellationToken = default);

    /// <summary>更新权限点</summary>
    Task<PermissionDto> UpdateAsync(Guid id, UpdatePermissionDto dto, CancellationToken cancellationToken = default);

    /// <summary>删除权限点（物理删除，级联清理 RolePermission 和 UserPermission 关联）</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
