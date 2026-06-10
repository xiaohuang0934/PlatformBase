namespace PlatformBase.Core.Entities;

/// <summary>
/// 角色-权限关联实体，复合主键 (RoleId, PermissionId)
/// 角色拥有的权限集合，用户通过被赋予角色来继承对应权限
/// </summary>
public class RolePermission
{
    /// <summary>角色 ID，关联 <see cref="Role"/></summary>
    public Guid RoleId { get; set; }

    /// <summary>权限 ID，关联 <see cref="Permission"/></summary>
    public Guid PermissionId { get; set; }
}
