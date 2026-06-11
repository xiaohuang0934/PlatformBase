namespace PlatformBase.Core.Entities;

/// <summary>
/// 组织架构实体（树形部门管理）
/// 使用物化路径（Materialized Path）存储完整层级路径，如 /1/4/7/
/// </summary>
public class OrganizationUnit : TenantSoftDeleteEntity
{
    /// <summary>部门名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>部门编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>物化路径（如 /1/4/7/），用于快速查询所有子部门</summary>
    public string Path { get; set; } = "/";

    /// <summary>上级部门 ID（null=顶级）</summary>
    public Guid? ParentId { get; set; }

    /// <summary>排序</summary>
    public int SortOrder { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;
}
