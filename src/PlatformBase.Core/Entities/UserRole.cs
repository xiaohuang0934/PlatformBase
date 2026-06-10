namespace PlatformBase.Core.Entities;

/// <summary>
/// 用户-角色关联实体，复合主键 (UserId, RoleId)
/// 表示一个用户被赋予的角色的多对多关系
/// </summary>
public class UserRole
{
    /// <summary>用户 ID，关联 <see cref="User"/></summary>
    public Guid UserId { get; set; }

    /// <summary>角色 ID，关联 <see cref="Role"/></summary>
    public Guid RoleId { get; set; }
}
