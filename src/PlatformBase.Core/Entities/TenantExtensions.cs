namespace PlatformBase.Core.Entities;

/// <summary>
/// 用户类型枚举
/// </summary>
public enum UserType
{
    /// <summary>平台管理员（跨租户）</summary>
    PlatformAdmin = 1,

    /// <summary>租户管理员</summary>
    TenantAdmin = 2,

    /// <summary>租户普通用户</summary>
    TenantUser = 3
}

// ===== 扩展已有实体 =====

/// <summary>User 扩展字段</summary>
public partial class User
{
    /// <summary>所属租户 ID（null=平台账号）</summary>
    public Guid? TenantId { get; set; }

    /// <summary>用户类型</summary>
    public UserType UserType { get; set; } = UserType.TenantUser;
}

/// <summary>Role 扩展字段</summary>
public partial class Role
{
    /// <summary>所属租户 ID（null=全局角色，如 Admin）</summary>
    public Guid? TenantId { get; set; }
}
