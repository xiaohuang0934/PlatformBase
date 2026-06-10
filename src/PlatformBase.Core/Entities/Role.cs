namespace PlatformBase.Core.Entities;

/// <summary>
/// 系统角色实体，继承 <see cref="AuditableEntity"/> 自动获得创建/修改审计追踪
/// 角色是权限的集合载体，通过 <see cref="RolePermission"/> 关联到具体 API 权限
/// </summary>
public class Role : AuditableEntity
{
    /// <summary>角色名称，全局唯一（如：Admin / Manager / User）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>规范化角色名（大写），用于大小写不敏感的快速查找</summary>
    public string NormalizedName { get; set; } = string.Empty;

    /// <summary>角色描述</summary>
    public string? Description { get; set; }
}
