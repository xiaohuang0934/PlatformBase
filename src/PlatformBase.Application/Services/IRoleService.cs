using PlatformBase.Application.Dtos;
using PlatformBase.Core.Models;

namespace PlatformBase.Application.Services;

/// <summary>
/// 角色管理服务接口，提供角色的完整 CRUD 操作
/// </summary>
public interface IRoleService
{
    /// <summary>分页查询角色列表</summary>
    Task<PagedResult<RoleDto>> GetPagedAsync(RoleQuery query, CancellationToken cancellationToken = default);

    /// <summary>根据 ID 查询角色详情</summary>
    Task<RoleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>创建角色</summary>
    Task<RoleDto> CreateAsync(CreateRoleDto dto, CancellationToken cancellationToken = default);

    /// <summary>更新角色</summary>
    Task<RoleDto> UpdateAsync(Guid id, UpdateRoleDto dto, CancellationToken cancellationToken = default);

    /// <summary>删除角色（物理删除，需先确保无用户关联）</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>查询角色拥有的权限编码列表</summary>
    Task<IReadOnlyList<string>> GetPermissionCodesAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>给角色批量分配权限（全量替换）</summary>
    Task AssignPermissionsAsync(Guid roleId, IReadOnlyList<string> permissionCodes, CancellationToken cancellationToken = default);
}
