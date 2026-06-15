namespace PlatformBase.Core.Entities;

/// <summary>
/// 用户-组织架构多对多关联表
/// 复合主键：(UserId, OrganizationUnitId)
/// </summary>
public class UserOrganizationUnit
{
    /// <summary>用户 ID，关联 User</summary>
    public Guid UserId { get; set; }

    /// <summary>组织架构 ID，关联 OrganizationUnit</summary>
    public Guid OrganizationUnitId { get; set; }
}