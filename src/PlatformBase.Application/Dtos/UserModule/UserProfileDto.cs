using PlatformBase.Core.Entities;

namespace PlatformBase.Application.Dtos.UserModule;

/// <summary>
/// 用户资料返回 DTO，不包含密码哈希、安全戳等敏感字段
/// </summary>
public class UserProfileDto
{
    /// <summary>用户 ID</summary>
    public Guid Id { get; set; }

    /// <summary>用户名</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>邮箱</summary>
    public string? Email { get; set; }

    /// <summary>邮箱是否已验证</summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>手机号</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>账户是否启用</summary>
    public bool IsActive { get; set; }

    /// <summary>用户拥有的角色名称列表</summary>
    public IReadOnlyList<string> Roles { get; set; } = [];

    /// <summary>账户创建时间</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>最后修改时间</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>用户类型</summary>
    public UserType UserType { get; set; }
}
