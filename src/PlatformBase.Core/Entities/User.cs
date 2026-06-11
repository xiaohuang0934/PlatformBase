namespace PlatformBase.Core.Entities;

/// <summary>
/// 系统用户实体，继承 <see cref="SoftDeleteEntity"/> 自动获得：
/// <list type="bullet">
///   <item>Guid 主键（<see cref="BaseEntity"/>）</item>
///   <item>创建/修改审计追踪（<see cref="AuditableEntity"/>）</item>
///   <item>软删除支持（<see cref="SoftDeleteEntity"/>）</item>
/// </list>
/// </summary>
public partial class User : SoftDeleteEntity
{
    /// <summary>登录用户名，全局唯一</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>规范化用户名（大写），用于大小写不敏感的快速查找</summary>
    public string NormalizedUsername { get; set; } = string.Empty;

    /// <summary>电子邮箱地址</summary>
    public string? Email { get; set; }

    /// <summary>规范化邮箱（大写），用于大小写不敏感查找</summary>
    public string? NormalizedEmail { get; set; }

    /// <summary>邮箱是否已验证</summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>BCrypt 格式密码哈希（包含算法标识+盐值+哈希结果）</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 安全戳，密码变更 / 角色变更时更新
    /// 可用于使已签发的旧 JWT / RefreshToken 失效
    /// </summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString();

    /// <summary>手机号码</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>是否开启两步验证（预留）</summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>账户锁定到期时间，NULL 表示未锁定</summary>
    public DateTimeOffset? LockoutEnd { get; set; }

    /// <summary>是否启用登录失败锁定机制</summary>
    public bool LockoutEnabled { get; set; } = true;

    /// <summary>累计登录失败次数，成功后归零</summary>
    public int AccessFailedCount { get; set; }

    /// <summary>账户是否启用（停用则无法登录）</summary>
    public bool IsActive { get; set; } = true;
}
