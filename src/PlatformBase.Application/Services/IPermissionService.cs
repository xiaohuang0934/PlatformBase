using PlatformBase.Application.Dtos;
using PlatformBase.Core.Models;

namespace PlatformBase.Application.Services;

/// <summary>
/// 权限服务接口，负责权限鉴权查询 + 权限点的完整 CRUD 管理
/// 鉴权查询优先使用 Redis 缓存，缓存未命中时查询数据库并回写缓存
/// </summary>
public interface IPermissionService
{
    /// <summary>获取指定用户的所有权限编码列表（合并角色权限 + 用户直达权限，含拒绝覆盖逻辑）</summary>
    Task<IReadOnlyList<string>> GetUserPermissionCodesAsync(Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>判断用户是否拥有指定权限编码</summary>
    Task<bool> HasPermissionAsync(Guid userId, string permissionCode,
        CancellationToken cancellationToken = default);

    /// <summary>清除指定用户的权限缓存</summary>
    Task InvalidateUserCacheAsync(Guid userId, CancellationToken cancellationToken = default);

    // ───── 权限点 CRUD 管理（v1.1）─────

    /// <summary>分页查询权限列表</summary>
    Task<PagedResult<PermissionDto>> GetPagedAsync(PermissionQuery query, CancellationToken cancellationToken = default);

    /// <summary>根据 ID 查询权限详情</summary>
    Task<PermissionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>创建权限点</summary>
    Task<PermissionDto> CreateAsync(CreatePermissionDto dto, CancellationToken cancellationToken = default);

    /// <summary>更新权限点</summary>
    Task<PermissionDto> UpdateAsync(Guid id, UpdatePermissionDto dto, CancellationToken cancellationToken = default);

    /// <summary>删除权限点（物理删除，级联清理关联）</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
