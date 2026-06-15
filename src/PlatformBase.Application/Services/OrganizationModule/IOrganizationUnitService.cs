using PlatformBase.Core.Entities;
using PlatformBase.Core.Models;

namespace PlatformBase.Application.Services.OrganizationModule;

/// <summary>
/// 组织架构服务接口，提供树形部门的 CRUD（物化路径）
/// </summary>
public interface IOrganizationUnitService
{
    /// <summary>
    /// 获取租户摘要列表（无参调用时返回顶层节点）
    /// - 平台用户：返回已分配租户列表
    /// - 租户用户：返回自己租户的单条摘要
    /// </summary>
    Task<IReadOnlyList<TenantOrgSummary>> GetTenantSummariesAsync(CancellationToken ct = default);

    /// <summary>
    /// 懒加载获取部门列表
    /// - tenantId 指定时，仅查询该租户下的部门
    /// - parentId 指定时，仅返回该部门的直接子级（默认返回根节点 parentId=null）
    /// </summary>
    Task<IReadOnlyList<OrgUnitNode>> GetTreeAsync(Guid? tenantId = null, Guid? parentId = null, CancellationToken ct = default);

    /// <summary>获取部门全量树（用于 OrgSelector 等需要完整树的组件）</summary>
    Task<IReadOnlyList<OrgUnitNode>> GetFullTreeAsync(CancellationToken ct = default);

    /// <summary>根据ID查询部门</summary>
    Task<OrganizationUnit?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>创建部门（自动计算物化路径）</summary>
    Task<OrganizationUnit> CreateAsync(string name, string code, Guid? parentId, int sortOrder, CancellationToken ct = default);
    /// <summary>更新部门名称/父级/排序</summary>
    Task<OrganizationUnit> UpdateAsync(Guid id, string? name, Guid? parentId, int? sortOrder, CancellationToken ct = default);
    /// <summary>删除部门（软删除）</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>根据ID列表批量查询部门</summary>
    Task<List<OrganizationUnit>> GetByIdsAsync(List<Guid> ids, CancellationToken ct = default);

    // ───── 部门用户查询 ─────

    /// <summary>查询部门下的用户（仅本部门）</summary>
    Task<IReadOnlyList<User>> GetUsersAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);

    /// <summary>查询部门及其子级部门的所有用户</summary>
    Task<IReadOnlyList<User>> GetUsersWithChildrenAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);

    /// <summary>分页查询部门及其子级部门的用户</summary>
    Task<PagedResult<UserDto>> GetUsersPagedAsync(Guid organizationUnitId, UserQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// 租户组织摘要（组织架构顶层节点，表示一个租户）
/// </summary>
public class TenantOrgSummary
{
    /// <summary>租户ID</summary>
    public Guid TenantId { get; set; }
    /// <summary>租户名称</summary>
    public string TenantName { get; set; } = string.Empty;
    /// <summary>租户编码</summary>
    public string TenantCode { get; set; } = string.Empty;
    /// <summary>是否有子级部门</summary>
    public bool HasChildren { get; set; }
}

/// <summary>
/// 组织架构树节点（返回给前端）
/// </summary>
public class OrgUnitNode
{
    /// <summary>部门ID</summary>
    public Guid Id { get; set; }
    /// <summary>部门名称</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>部门编码</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>上级部门ID</summary>
    public Guid? ParentId { get; set; }
    /// <summary>排序</summary>
    public int SortOrder { get; set; }
    /// <summary>是否有子级部门（用于前端懒加载展开图标）</summary>
    public bool HasChildren { get; set; }
    /// <summary>子部门树（仅 mode=tree 全量模式时填充）</summary>
    public List<OrgUnitNode> Children { get; set; } = [];
}
