using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Services;
using PlatformBase.Infrastructure.Data;

namespace PlatformBase.Host.Services.UserModule;

/// <summary>
/// 当前请求的用户上下文实现（身份提取 + 租户范围 + 多租户支持）。
///
/// 身份信息：从 HttpContext.User (JWT ClaimsPrincipal) 中提取。
/// 租户范围：
///   - 租户用户：CurrentTenantId = 自身 tenant_id（不可切换）
///   - 平台管理员：调用 SetCurrentTenant(Guid?) 切换租户视角
///   - AccessibleTenantIds 控制 EF Core 全局过滤器可见范围
/// </summary>
public class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private List<Guid>? _accessibleIds;
    private Guid? _currentTenantId;

    public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    // ═══════════════════ 身份信息（只读，来自 JWT Claims）═══════════════════

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? UserName => User?.FindFirstValue(ClaimTypes.Name);

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    public IReadOnlyList<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public string? IpAddress =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? ClientId => User?.FindFirstValue("client_id");

    public bool IsSuperAdmin =>
        int.TryParse(User?.FindFirstValue("user_type"), out var ut)
        && ut == (int)UserType.PlatformAdmin;

    // ═══════════════════ 租户范围 ═══════════════════

    /// <summary>
    /// 当前租户 ID。
    /// 优先级：SetCurrentTenant 设置的值 > 平台管理员未设置 = null
    /// > 租户用户自身的 TenantId。
    /// </summary>
    public Guid? CurrentTenantId
    {
        get
        {
            if (_currentTenantId != null)
                return _currentTenantId;

            if (!IsAuthenticated || UserId == null)
                return null;

            if (IsSuperAdmin)
                return null;

            var raw = User?.FindFirstValue("tenant_id");
            return Guid.TryParse(raw, out var tid) ? tid : null;
        }
    }

    /// <summary>
    /// 设置当前请求的租户视角。仅平台管理员可调用。
    /// 设置后自动清除 AccessibleTenantIds 缓存。
    /// </summary>
    public void SetCurrentTenant(Guid? tenantId)
    {
        if (!IsSuperAdmin) return;

        _currentTenantId = tenantId;
        _accessibleIds = null;
    }

    /// <summary>
    /// 可访问的租户列表：
    ///   - 平台管理员 + SetCurrentTenant 已设 → [该租户]
    ///   - 平台管理员 + 未设 → PlatformUserTenants 表中全部已分配
    ///   - 租户用户 → [自身租户]
    /// </summary>
    public IReadOnlyList<Guid> AccessibleTenantIds
    {
        get
        {
            if (_accessibleIds != null)
                return _accessibleIds;

            if (!IsAuthenticated || UserId == null)
                return _accessibleIds = [];

            if (IsSuperAdmin && _currentTenantId != null)
                return _accessibleIds = [_currentTenantId.Value];

            if (IsSuperAdmin)
                return _accessibleIds = LoadAssignedTenants();

            var raw = User?.FindFirstValue("tenant_id");
            _accessibleIds = Guid.TryParse(raw, out var tid) ? [tid] : [];
            return _accessibleIds;
        }
    }

    /// <summary>
    /// 校验是否有权访问指定租户。
    /// 平台管理员 → 检查 PlatformUserTenants 分配关系；
    /// 租户用户 → 只能访问自身租户。
    /// </summary>
    public bool HasAccess(Guid tenantId)
    {
        if (!IsAuthenticated || UserId == null)
            return false;

        if (IsSuperAdmin)
        {
            _accessibleIds ??= LoadAssignedTenants();
            return _accessibleIds.Contains(tenantId);
        }

        var raw = User?.FindFirstValue("tenant_id");
        return Guid.TryParse(raw, out var tid) && tid == tenantId;
    }

    /// <summary>
    /// 从 PlatformUserTenants 表加载平台管理员已分配的租户 ID 列表。
    /// 通过 IServiceProvider 延迟解析 AppDbContext，避免构造函数循环依赖。
    /// 查询失败时返回空列表，不中断请求。
    /// </summary>
    private List<Guid> LoadAssignedTenants()
    {
        try
        {
            var db = _httpContextAccessor.HttpContext?.RequestServices
                .GetService<AppDbContext>();
            if (db == null) return [];

            return db.PlatformUserTenants
                .AsNoTracking()
                .Where(p => p.PlatformUserId == UserId!.Value)
                .Select(p => p.TenantId)
                .ToList();
        }
        catch { return []; }
    }
}
