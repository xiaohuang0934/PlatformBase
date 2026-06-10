namespace PlatformBase.Core.Entities;

/// <summary>
/// 用户直达权限实体，复合主键 (UserId, PermissionId)
/// 允许直接给单个用户赋予或拒绝特定权限，而不依赖角色体系
/// 用户直达权限的优先级高于角色继承的权限
/// </summary>
public class UserPermission
{
    /// <summary>用户 ID，关联 <see cref="User"/></summary>
    public Guid UserId { get; set; }

    /// <summary>权限 ID，关联 <see cref="Permission"/></summary>
    public Guid PermissionId { get; set; }

    /// <summary>
    /// 是否授权
    /// <para>true  — 授权：用户拥有该权限（无论角色是否有）</para>
    /// <para>false — 显式拒绝：即使角色拥有该权限，用户也无法使用</para>
    /// <para>优先级：UserPermission (true/false) 高于 RolePermission</para>
    /// </summary>
    public bool IsGranted { get; set; } = true;
}
