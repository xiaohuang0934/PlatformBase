using PlatformBase.Core.Entities;

namespace PlatformBase.Application.Services.OrganizationModule;

/// <summary>
/// 组织架构服务接口，提供树形部门的 CRUD（物化路径）
/// </summary>
public interface IOrganizationUnitService
{
    /// <summary>获取部门树形列表</summary>
    Task<IReadOnlyList<OrgUnitNode>> GetTreeAsync(CancellationToken ct = default);
    /// <summary>根据ID查询部门</summary>
    Task<OrganizationUnit?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>创建部门（自动计算物化路径）</summary>
    Task<OrganizationUnit> CreateAsync(string name, string code, Guid? parentId, int sortOrder, CancellationToken ct = default);
    /// <summary>更新部门名称/父级/排序</summary>
    Task<OrganizationUnit> UpdateAsync(Guid id, string? name, Guid? parentId, int? sortOrder, CancellationToken ct = default);
    /// <summary>删除部门（软删除）</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
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
    /// <summary>子部门列表</summary>
    public List<OrgUnitNode> Children { get; set; } = [];
}
