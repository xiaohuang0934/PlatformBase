using PlatformBase.Core.Entities;

namespace PlatformBase.Core.Services;

/// <summary>
/// 当前请求的用户上下文（合并身份 + 租户两方面信息）。
/// 在应用的任何层次（Controller / Service / DbContext）均可注入使用。
///
/// 身份信息（只读，来自 JWT Claims）：
///   UserId / UserName / IsAuthenticated / IpAddress / ClientId / UserType
///
/// 租户范围（DB 查询 + Redis 缓存）：
///   TenantId（归属租户）/ CurrentTenantId（当前视角）/ TenantIds（平台用户分配列表）
///
/// 安全约束：
///   - SetCurrentTenant 仅平台用户可调用（租户用户调用被忽略）
///   - HasAccess 校验用户是否有权访问指定租户的数据
/// </summary>
public interface ICurrentUserContext
{
    // ═══════ 身份信息（只读，来自 JWT Claims）═══════

    Guid? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
    string? IpAddress { get; }
    string? ClientId { get; }

    /// <summary>
    /// 用户类型（从 JWT user_type Claim 解析）：
    /// PlatformAdmin = 1（平台管理员）
    /// TenantAdmin = 2（租户管理员）
    /// TenantUser = 3（租户普通用户）
    /// </summary>
    UserType UserType { get; }

    /// <summary>是否超级管理员（绕过所有权限检查和数据过滤）</summary>
    bool IsSuperAdmin { get; }

    // ═══════ 租户范围（DB 查询 + Redis 缓存）═══════

    /// <summary>
    /// 归属租户 ID：
    /// - 租户用户 = User.TenantId（固定，DB 查询）
    /// - 平台用户 = null
    /// </summary>
    Guid? TenantId { get; }

    /// <summary>
    /// 当前视角租户 ID（用于数据隔离过滤）：
    /// - 租户用户 = TenantId（固定）
    /// - 平台用户 = SetCurrentTenant 设置值 / null（未设置时使用 TenantIds）
    /// </summary>
    Guid? CurrentTenantId { get; }

    /// <summary>
    /// 可访问的租户 ID 列表：
    /// - 平台用户 = PlatformUserTenants 表分配列表（DB 查询 + Redis 缓存）
    /// - 租户用户 = []（空列表）
    /// </summary>
    IReadOnlyList<Guid> TenantIds { get; }

    // ═══════ 操作方法 ═══════

    /// <summary>
    /// 设置当前请求的租户视角。仅平台用户可调用此方法切换租户；
    /// 租户用户调用会被忽略（安全保护）。
    /// </summary>
    /// <param name="tenantId">目标租户 ID（null = 清除设置，回退到 TenantIds）</param>
    void SetCurrentTenant(Guid? tenantId);

    /// <summary>
    /// 校验当前用户是否有权访问指定租户的数据。
    /// 平台用户 → 校验该租户是否在 TenantIds 中；
    /// 租户用户 → 直接比对自身 TenantId。
    /// </summary>
    /// <param name="tenantId">待校验的租户 ID</param>
    /// <returns>true = 有权访问</returns>
    bool HasAccess(Guid tenantId);

    /// <summary>
    /// 设置当前请求的多租户视角。仅平台用户可调用。
    /// 用于查询任意 N 个租户的场景（如查询 2 个指定租户）。
    /// </summary>
    /// <param name="tenantIds">目标租户 ID 列表（null = 清除设置，回退到 TenantIds）</param>
    void SetCurrentTenants(IReadOnlyList<Guid>? tenantIds);

    /// <summary>
    /// 当前生效的租户 ID 列表（用于数据隔离过滤）：
    /// - 租户用户 = [TenantId]
    /// - 平台用户 = SetCurrentTenants 设置值 / [CurrentTenantId] / TenantIds
    /// </summary>
    IReadOnlyList<Guid> CurrentTenantIds { get; }

    /// <summary>
    /// 失效当前用户的租户缓存。
    /// 当用户的 TenantId 变更或平台用户的租户分配变更时调用，
    /// 下次请求将重新从数据库加载租户信息。
    /// </summary>
    void InvalidateTenantCache();

    // ═══════════════════ 部门范围（DB 查询 + Redis 缓存）═══════════════════

    /// <summary>
    /// 用户所属部门 ID 列表：
    /// - 从 UserOrganizationUnit 表查询用户关联的部门
    /// - 多对多关系，用户可属于多个部门
    /// </summary>
    IReadOnlyList<Guid> OrganizationUnitIds { get; }

    /// <summary>
    /// 可访问的部门物化路径列表（用于数据权限过滤）：
    /// - 用户所属部门的物化路径
    /// - 用于 LIKE '{path}%' 查询本部门及所有子级部门
    /// </summary>
    IReadOnlyList<string> AccessibleOrgPaths { get; }

    /// <summary>
    /// 失效当前用户的部门缓存。
    /// 当用户的部门关联变更时调用，下次请求将重新从数据库加载部门信息。
    /// </summary>
    void InvalidateOrganizationCache();
}