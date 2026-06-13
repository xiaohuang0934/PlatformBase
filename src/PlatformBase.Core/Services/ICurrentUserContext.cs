namespace PlatformBase.Core.Services;

/// <summary>
/// 当前请求的用户上下文（合并身份 + 租户两方面信息）。
/// 在应用的任何层次（Controller / Service / DbContext）均可注入使用。
/// 
/// 身份信息（只读，来自 JWT Claims）：
///   UserId / UserName / Email / Roles / IsAuthenticated / IpAddress / ClientId / IsSuperAdmin
///
/// 租户范围（允许 Controller 通过 SetCurrentTenant 写入）：
///   CurrentTenantId / AccessibleTenantIds
///
/// 安全约束：
///   - SetCurrentTenant 仅平台管理员可调用（租户用户调用被忽略）
///   - HasAccess 校验用户是否有权访问指定租户的数据
/// </summary>
public interface ICurrentUserContext
{
    // ═══════ 身份信息（只读，来自 JWT Claims）═══════

    Guid? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }
    string? IpAddress { get; }
    string? ClientId { get; }
    bool IsSuperAdmin { get; }

    // ═══════ 租户范围（Controller 可写）═══════

    /// <summary>当前租户 ID（平台管理员可通过 SetCurrentTenant 切换）</summary>
    Guid? CurrentTenantId { get; }

    /// <summary>可访问的租户 ID 列表（用于 EF Core 全局查询过滤器）</summary>
    IReadOnlyList<Guid> AccessibleTenantIds { get; }

    // ═══════ 操作方法 ═══════

    /// <summary>
    /// 设置当前请求的租户视角。仅平台管理员可调用此方法切换租户；
    /// 租户用户调用会被忽略（安全保护）。
    /// </summary>
    /// <param name="tenantId">目标租户 ID（null = 清除设置，回退到默认逻辑）</param>
    void SetCurrentTenant(Guid? tenantId);

    /// <summary>
    /// 校验当前用户是否有权访问指定租户的数据。
    /// 平台管理员 → 校验该租户是否在 PlatformUserTenants 表中已分配；
    /// 租户用户 → 直接比对自身 TenantId。
    /// </summary>
    /// <param name="tenantId">待校验的租户 ID</param>
    /// <returns>true = 有权访问</returns>
    bool HasAccess(Guid tenantId);
}
