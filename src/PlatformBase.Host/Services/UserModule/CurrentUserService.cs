using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Services;
using PlatformBase.Infrastructure.Data;

namespace PlatformBase.Host.Services.UserModule;

/// <summary>
/// 当前用户会话上下文实现（含多租户支持）
/// 平台管理员可通过 X-Tenant-Id 请求头切换租户视角
/// AccessibleTenantIds 控制读取可见范围（已分配租户）
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private List<Guid>? _accessibleIds; // 缓存，避免重复查 DB

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? UserName => User?.FindFirstValue(ClaimTypes.Name);

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    public IReadOnlyList<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? ClientId => User?.FindFirstValue("client_id");

    public bool IsSuperAdmin =>
        int.TryParse(User?.FindFirstValue("user_type"), out var ut) && ut == (int)UserType.PlatformAdmin;

    public Guid? TenantId
    {
        get
        {
            var newTid = ComputeTenantId();
            // 如果 TenantId 变化，清除 AccessibleTenantIds 缓存
            if (_cachedTenantId != newTid)
            {
                _cachedTenantId = newTid;
                _accessibleIds = null;
            }
            return newTid;
        }
    }
    private Guid? _cachedTenantId;

    /// <summary>根据用户类型计算当前租户 ID（平台管理员=请求头 / 租户用户=JWT Claim）</summary>
    private Guid? ComputeTenantId()
    {
        if (!IsSuperAdmin)
        {
            var raw = User?.FindFirstValue("tenant_id");
            return Guid.TryParse(raw, out var tid) ? tid : null;
        }
        var header = _httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        return Guid.TryParse(header, out var htid) ? htid : null;
    }

    /// <summary>
    /// 可访问的租户 ID 列表
    /// 平台管理员（无 X-Tenant-Id）= PlatformUserTenants 表中已分配的
    /// 平台管理员（有 X-Tenant-Id）= 当前切换到的单个租户
    /// 租户用户 = 自己归属的租户
    /// </summary>
    public IReadOnlyList<Guid> AccessibleTenantIds
    {
        get
        {
            if (_accessibleIds != null) return _accessibleIds;

            if (!IsAuthenticated || UserId == null)
                return _accessibleIds = [];

            if (!IsSuperAdmin)
            {
                // 租户用户：只看自己租户
                var raw = User?.FindFirstValue("tenant_id");
                _accessibleIds = Guid.TryParse(raw, out var tid) ? [tid] : [];
            }
            else if (TenantId != null)
            {
                // 平台管理员选了具体租户
                _accessibleIds = [TenantId.Value];
            }
            else
            {
                // 平台管理员未选租户 → 查已分配列表
                _accessibleIds = LoadAssignedTenants();
            }

            return _accessibleIds;
        }
    }

    /// <summary>
    /// 从 PlatformUserTenants 表加载平台管理员已分配的租户 ID 列表
    /// 通过 IServiceProvider 懒惰解析 AppDbContext，避免构造函数循环依赖
    /// </summary>
    private List<Guid> LoadAssignedTenants()
    {
        try
        {
            var db = _httpContextAccessor.HttpContext?.RequestServices.GetService<AppDbContext>();
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
