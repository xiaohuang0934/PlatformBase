namespace PlatformBase.Core.Services;

/// <summary>
/// 当前用户会话上下文接口，提供当前登录用户的身份信息
/// 在应用的任何层次（Controller / Service / DbContext）均可注入使用
/// </summary>
public interface ICurrentUserService
{
    /// <summary>当前登录用户的全局唯一标识，未登录时返回 null</summary>
    Guid? UserId { get; }

    /// <summary>当前登录用户名，未登录时返回 null</summary>
    string? UserName { get; }

    /// <summary>当前登录用户邮箱，未登录时返回 null</summary>
    string? Email { get; }

    /// <summary>当前用户拥有的角色名称列表，未登录时返回空列表</summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>当前请求是否已通过认证（JWT 验证通过）</summary>
    bool IsAuthenticated { get; }

    /// <summary>客户端 IP 地址，用于审计和限流</summary>
    string? IpAddress { get; }

    /// <summary>发起当前请求的 OAuth2 客户端 ID（来自 IdentityServer4）</summary>
    string? ClientId { get; }

    // ───── 多租户扩展 ─────

    /// <summary>当前租户 ID（平台账号未选择租户时为 null）</summary>
    Guid? TenantId { get; }

    /// <summary>是否为平台管理员（跨租户查看）</summary>
    bool IsSuperAdmin { get; }

    // ───── 多租户读取 ─────

    /// <summary>可访问的租户 ID 列表（用于全局过滤器）
    /// 平台管理员（无X-Tenant-Id）= 已分配的租户列表
    /// 平台管理员（有X-Tenant-Id）= 单个租户
    /// 租户用户 = 自己归属的租户</summary>
    IReadOnlyList<Guid> AccessibleTenantIds { get; }
}
