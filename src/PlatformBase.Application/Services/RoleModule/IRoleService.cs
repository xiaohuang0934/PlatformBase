using PlatformBase.Core.Models;

namespace PlatformBase.Application.Services.RoleModule;

/// <summary>
/// 角色管理服务接口，提供角色的完整 CRUD 和权限分配
/// </summary>
public interface IRoleService
{
    /// <summary>分页查询角色列表（含租户过滤）</summary>
    /// <param name="query">查询条件（关键词、分页参数）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果</returns>
    Task<PagedResult<RoleDto>> GetPagedAsync(RoleQuery query, CancellationToken cancellationToken = default);

    /// <summary>根据 ID 查询角色</summary>
    /// <param name="id">角色 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色 DTO，未找到返回 null</returns>
    Task<RoleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>创建角色（自动填入当前请求的 TenantId）</summary>
    /// <param name="dto">创建请求（名称、描述）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的角色 DTO</returns>
    Task<RoleDto> CreateAsync(CreateRoleDto dto, CancellationToken cancellationToken = default);

    /// <summary>更新角色</summary>
    /// <param name="id">角色 ID</param>
    /// <param name="dto">更新请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的角色 DTO</returns>
    Task<RoleDto> UpdateAsync(Guid id, UpdateRoleDto dto, CancellationToken cancellationToken = default);

    /// <summary>删除角色（物理删除），如有用户关联则拒绝</summary>
    /// <param name="id">角色 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>查询角色拥有的权限编码列表</summary>
    /// <param name="roleId">角色 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限编码列表</returns>
    Task<IReadOnlyList<string>> GetPermissionCodesAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>给角色批量分配权限（全量替换 + 事务保护）</summary>
    /// <param name="roleId">角色 ID</param>
    /// <param name="permissionCodes">权限编码列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AssignPermissionsAsync(Guid roleId, IReadOnlyList<string> permissionCodes, CancellationToken cancellationToken = default);
}
