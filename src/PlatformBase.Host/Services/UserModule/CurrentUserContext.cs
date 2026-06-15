using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Services;
using PlatformBase.Infrastructure.Data;
using StackExchange.Redis;

namespace PlatformBase.Host.Services.UserModule;

/// <summary>
/// 当前请求的用户上下文实现（身份提取 + 租户范围 + 多租户支持）。
///
/// 身份信息：从 HttpContext.User (JWT ClaimsPrincipal) 中提取。
/// 租户范围：
///   - 租户用户：TenantId = User.TenantId（DB 查询 + Redis 缓存），TenantIds = []
///   - 平台用户：TenantId = null，TenantIds = PlatformUserTenants 分配列表（DB + Redis 缓存）
///   - CurrentTenantId：租户用户 = TenantId；平台用户 = SetCurrentTenant 设置值 / null
///   - CurrentTenantIds：用于数据隔离，支持单租户聚焦或多租户聚合
/// </summary>
public class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _currentTenantId;
    private List<Guid>? _currentTenantIdsOverride;
    private Guid? _cachedTenantId;
    private List<Guid>? _cachedTenantIds;
    private bool _tenantLoaded;

    private List<Guid>? _cachedOrgIds;
    private List<string>? _cachedOrgPaths;
    private bool _orgLoaded;

    private bool? _cachedIsSuperAdmin;
    private bool _superAdminLoaded;

    private const string TenantCacheKeyPrefix = "user:tenant:";
    private const string OrgCacheKeyPrefix = "user:org:";
    private const string SuperAdminCacheKeyPrefix = "user:superadmin:";
    private const int TenantCacheTtlMinutes = 30;
    private const int OrgCacheTtlMinutes = 30;
    private const int SuperAdminCacheTtlMinutes = 30;

    public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    // ═══════════════════ 身份信息（只读，来自 JWT Claims）═══════════════════

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? UserName => User?.FindFirstValue(ClaimTypes.Name);

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public string? IpAddress =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? ClientId => User?.FindFirstValue("client_id");

    /// <summary>
    /// 用户类型（从 JWT user_type Claim 解析）
    /// </summary>
    public UserType UserType =>
        int.TryParse(User?.FindFirstValue("user_type"), out var ut)
        && Enum.IsDefined(typeof(UserType), ut)
            ? (UserType)ut
            : UserType.TenantUser;

    /// <summary>
    /// 是否超级管理员（从 DB 加载 + Redis 缓存）
    /// 超级管理员绕过所有权限检查和数据过滤
    /// </summary>
    public bool IsSuperAdmin
    {
        get
        {
            if (!IsAuthenticated || UserId == null)
                return false;

            EnsureSuperAdminCacheLoaded();
            return _cachedIsSuperAdmin ?? false;
        }
    }

    // ═══════════════════ 租户范围（DB 查询 + Redis 缓存）═══════════════════

    /// <summary>
    /// 归属租户 ID：
    /// - 租户用户 = User.TenantId（DB 查询 + Redis 缓存）
    /// - 平台用户 = null
    /// </summary>
    public Guid? TenantId
    {
        get
        {
            if (!IsAuthenticated || UserId == null)
                return null;

            if (UserType == UserType.PlatformAdmin)
                return null;

            EnsureTenantCacheLoaded();
            return _cachedTenantId;
        }
    }

    /// <summary>
    /// 当前视角租户 ID（用于单租户过滤）：
    /// - 租户用户 = TenantId（固定）
    /// - 平台用户 = SetCurrentTenant 设置值 / null
    /// </summary>
    public Guid? CurrentTenantId
    {
        get
        {
            if (!IsAuthenticated || UserId == null)
                return null;

            if (UserType != UserType.PlatformAdmin)
                return TenantId;

            return _currentTenantId;
        }
    }

    /// <summary>
    /// 平台用户分配的租户列表（DB 查询 + Redis 缓存）：
    /// - 平台用户 = PlatformUserTenants 表
    /// - 租户用户 = []
    /// </summary>
    public IReadOnlyList<Guid> TenantIds
    {
        get
        {
            if (!IsAuthenticated || UserId == null)
                return [];

            if (UserType != UserType.PlatformAdmin)
                return [];

            EnsureTenantCacheLoaded();
            return _cachedTenantIds ?? [];
        }
    }

    /// <summary>
    /// 当前生效的租户 ID 列表（用于数据隔离过滤）：
    /// - 租户用户 = [TenantId]
    /// - 平台用户 = SetCurrentTenants 设置值 / [CurrentTenantId] / TenantIds
    /// </summary>
    public IReadOnlyList<Guid> CurrentTenantIds
    {
        get
        {
            if (!IsAuthenticated || UserId == null)
                return [];

            if (UserType != UserType.PlatformAdmin)
                return TenantId != null ? [TenantId.Value] : [];

            if (_currentTenantIdsOverride != null)
                return _currentTenantIdsOverride;

            if (_currentTenantId != null)
                return [_currentTenantId.Value];

            return TenantIds;
        }
    }

    // ═══════════════════ 操作方法 ═══════════════════

    /// <summary>
    /// 设置当前请求的租户视角。仅平台用户可调用。
    /// </summary>
    public void SetCurrentTenant(Guid? tenantId)
    {
        if (UserType != UserType.PlatformAdmin) return;

        _currentTenantId = tenantId;
        _currentTenantIdsOverride = null;
    }

    /// <summary>
    /// 设置当前请求的多租户视角。仅平台用户可调用。
    /// 用于查询任意 N 个租户的场景。
    /// </summary>
    public void SetCurrentTenants(IReadOnlyList<Guid>? tenantIds)
    {
        if (UserType != UserType.PlatformAdmin) return;

        _currentTenantIdsOverride = tenantIds?.ToList();
        _currentTenantId = null;
    }

    /// <summary>
    /// 校验是否有权访问指定租户：
    /// - 平台用户 → 检查 TenantIds 是否包含
    /// - 租户用户 → 检查 TenantId 是否相等
    /// </summary>
    public bool HasAccess(Guid tenantId)
    {
        if (!IsAuthenticated || UserId == null)
            return false;

        if (UserType == UserType.PlatformAdmin)
            return TenantIds.Contains(tenantId);

        return TenantId == tenantId;
    }

    // ═══════════════════ 缓存加载逻辑 ═══════════════════

    /// <summary>
    /// 从 Redis 缓存或数据库加载租户信息。
    /// 缓存结构：{ "tenantId": "guid或null", "tenantIds": ["guid1","guid2"] }
    /// </summary>
    private void EnsureTenantCacheLoaded()
    {
        if (_tenantLoaded || UserId == null) return;

        var redis = _httpContextAccessor.HttpContext?.RequestServices
            .GetService<IConnectionMultiplexer>()?.GetDatabase();

        var cacheKey = $"{TenantCacheKeyPrefix}{UserId}";

        if (redis != null)
        {
            try
            {
                var cached = redis.StringGet(cacheKey);
                if (cached.HasValue)
                {
                    var data = JsonSerializer.Deserialize<TenantCacheData>(cached!);
                    if (data != null)
                    {
                        _cachedTenantId = data.TenantId;
                        _cachedTenantIds = data.TenantIds;
                        _tenantLoaded = true;
                        return;
                    }
                }
            }
            catch { }
        }

        LoadTenantFromDatabase(redis, cacheKey);
        _tenantLoaded = true;
    }

    /// <summary>
    /// 从数据库加载租户信息并回写 Redis 缓存
    /// </summary>
    private void LoadTenantFromDatabase(IDatabase? redis, string cacheKey)
    {
        var db = _httpContextAccessor.HttpContext?.RequestServices
            .GetService<AppDbContext>();

        if (db == null || UserId == null)
        {
            _cachedTenantId = null;
            _cachedTenantIds = [];
            return;
        }

        try
        {
            if (UserType == UserType.PlatformAdmin)
            {
                _cachedTenantId = null;
                _cachedTenantIds = db.PlatformUserTenants
                    .AsNoTracking()
                    .Where(p => p.PlatformUserId == UserId.Value)
                    .Select(p => p.TenantId)
                    .ToList();
            }
            else
            {
                var user = db.Users
                    .AsNoTracking()
                    .Where(u => u.Id == UserId.Value)
                    .Select(u => new { u.TenantId })
                    .FirstOrDefault();

                _cachedTenantId = user?.TenantId;
                _cachedTenantIds = [];
            }

            if (redis != null)
            {
                try
                {
                    var data = new TenantCacheData
                    {
                        TenantId = _cachedTenantId,
                        TenantIds = _cachedTenantIds ?? []
                    };
                    var json = JsonSerializer.Serialize(data);
                    redis.StringSet(cacheKey, json, TimeSpan.FromMinutes(TenantCacheTtlMinutes));
                }
                catch { }
            }
        }
        catch
        {
            _cachedTenantId = null;
            _cachedTenantIds = [];
        }
    }

    /// <summary>
    /// 失效当前用户的租户缓存。
    /// 删除 Redis key 并重置内部缓存标记，下次访问将重新从数据库加载。
    /// </summary>
    public void InvalidateTenantCache()
    {
        if (UserId == null) return;

        _tenantLoaded = false;
        _cachedTenantId = null;
        _cachedTenantIds = null;

        var redis = _httpContextAccessor.HttpContext?.RequestServices
            .GetService<IConnectionMultiplexer>()?.GetDatabase();

        if (redis != null)
        {
            try
            {
                redis.KeyDelete($"{TenantCacheKeyPrefix}{UserId}");
            }
            catch { }
        }
    }

    /// <summary>
    /// 缓存数据结构
    /// </summary>
    private class TenantCacheData
    {
        public Guid? TenantId { get; set; }
        public List<Guid> TenantIds { get; set; } = [];
    }

    // ═══════════════════ 部门范围（DB 查询 + Redis 缓存）═══════════════════

    /// <summary>
    /// 用户所属部门 ID 列表：
    /// - 从 UserOrganizationUnit 表查询用户关联的部门
    /// - 多对多关系，用户可属于多个部门
    /// - 平台管理员返回空列表（平台管理员不归属部门）
    /// </summary>
    public IReadOnlyList<Guid> OrganizationUnitIds
    {
        get
        {
            if (!IsAuthenticated || UserId == null || UserType == UserType.PlatformAdmin)
                return [];

            EnsureOrganizationCacheLoaded();
            return _cachedOrgIds ?? [];
        }
    }

    /// <summary>
    /// 可访问的部门物化路径列表（用于数据权限过滤）：
    /// - 用户所属部门的物化路径
    /// - 用于 LIKE '{path}%' 查询本部门及所有子级部门
    /// - 平台管理员返回空列表（平台管理员不归属部门）
    /// </summary>
    public IReadOnlyList<string> AccessibleOrgPaths
    {
        get
        {
            if (!IsAuthenticated || UserId == null || UserType == UserType.PlatformAdmin)
                return [];

            EnsureOrganizationCacheLoaded();
            return _cachedOrgPaths ?? [];
        }
    }

    /// <summary>
    /// 从 Redis 缓存或数据库加载部门信息。
    /// 缓存结构：{ "orgIds": ["guid1","guid2"], "orgPaths": ["path1","path2"] }
    /// </summary>
    private void EnsureOrganizationCacheLoaded()
    {
        if (_orgLoaded || UserId == null) return;

        var redis = _httpContextAccessor.HttpContext?.RequestServices
            .GetService<IConnectionMultiplexer>()?.GetDatabase();

        var cacheKey = $"{OrgCacheKeyPrefix}{UserId}";

        if (redis != null)
        {
            try
            {
                var cached = redis.StringGet(cacheKey);
                if (cached.HasValue)
                {
                    var data = JsonSerializer.Deserialize<OrganizationCacheData>(cached!);
                    if (data != null)
                    {
                        _cachedOrgIds = data.OrgIds;
                        _cachedOrgPaths = data.OrgPaths;
                        _orgLoaded = true;
                        return;
                    }
                }
            }
            catch { }
        }

        LoadOrganizationFromDatabase(redis, cacheKey);
        _orgLoaded = true;
    }

    /// <summary>
    /// 从数据库加载部门信息并回写 Redis 缓存
    /// </summary>
    private void LoadOrganizationFromDatabase(IDatabase? redis, string cacheKey)
    {
        var db = _httpContextAccessor.HttpContext?.RequestServices
            .GetService<AppDbContext>();

        if (db == null || UserId == null)
        {
            _cachedOrgIds = [];
            _cachedOrgPaths = [];
            return;
        }

        try
        {
            // 查询用户所属部门 ID 列表
            var orgIds = db.UserOrganizationUnits
                .AsNoTracking()
                .Where(uo => uo.UserId == UserId.Value)
                .Select(uo => uo.OrganizationUnitId)
                .ToList();

            // 查询这些部门的物化路径
            var orgPaths = orgIds.Count > 0
                ? db.OrganizationUnits
                    .AsNoTracking()
                    .Where(o => orgIds.Contains(o.Id) && !o.IsDeleted)
                    .Select(o => o.Path)
                    .ToList()
                : [];

            _cachedOrgIds = orgIds;
            _cachedOrgPaths = orgPaths;

            if (redis != null)
            {
                try
                {
                    var data = new OrganizationCacheData
                    {
                        OrgIds = _cachedOrgIds ?? [],
                        OrgPaths = _cachedOrgPaths ?? []
                    };
                    var json = JsonSerializer.Serialize(data);
                    redis.StringSet(cacheKey, json, TimeSpan.FromMinutes(OrgCacheTtlMinutes));
                }
                catch { }
            }
        }
        catch
        {
            _cachedOrgIds = [];
            _cachedOrgPaths = [];
        }
    }

    /// <summary>
    /// 失效当前用户的部门缓存。
    /// 删除 Redis key 并重置内部缓存标记，下次访问将重新从数据库加载。
    /// </summary>
    public void InvalidateOrganizationCache()
    {
        if (UserId == null) return;

        _orgLoaded = false;
        _cachedOrgIds = null;
        _cachedOrgPaths = null;

        var redis = _httpContextAccessor.HttpContext?.RequestServices
            .GetService<IConnectionMultiplexer>()?.GetDatabase();

        if (redis != null)
        {
            try
            {
                redis.KeyDelete($"{OrgCacheKeyPrefix}{UserId}");
            }
            catch { }
        }
    }

    /// <summary>
    /// 部门缓存数据结构
    /// </summary>
    private class OrganizationCacheData
    {
        public List<Guid> OrgIds { get; set; } = [];
        public List<string> OrgPaths { get; set; } = [];
    }

    // ═══════════════════ 超级管理员状态（DB 查询 + Redis 缓存）═══════════════════

    /// <summary>
    /// 从 Redis 缓存或数据库加载超级管理员状态
    /// </summary>
    private void EnsureSuperAdminCacheLoaded()
    {
        if (_superAdminLoaded || UserId == null) return;

        var redis = _httpContextAccessor.HttpContext?.RequestServices
            .GetService<IConnectionMultiplexer>()?.GetDatabase();

        var cacheKey = $"{SuperAdminCacheKeyPrefix}{UserId}";

        if (redis != null)
        {
            try
            {
                var cached = redis.StringGet(cacheKey);
                if (cached.HasValue && bool.TryParse(cached, out var isSuper))
                {
                    _cachedIsSuperAdmin = isSuper;
                    _superAdminLoaded = true;
                    return;
                }
            }
            catch { }
        }

        LoadSuperAdminFromDatabase(redis, cacheKey);
        _superAdminLoaded = true;
    }

    /// <summary>
    /// 从数据库加载超级管理员状态并回写 Redis 缓存
    /// </summary>
    private void LoadSuperAdminFromDatabase(IDatabase? redis, string cacheKey)
    {
        var db = _httpContextAccessor.HttpContext?.RequestServices
            .GetService<AppDbContext>();

        if (db == null || UserId == null)
        {
            _cachedIsSuperAdmin = false;
            return;
        }

        try
        {
            _cachedIsSuperAdmin = db.Users
                .AsNoTracking()
                .Where(u => u.Id == UserId.Value)
                .Select(u => u.IsSuperAdmin)
                .FirstOrDefault();

            if (redis != null)
            {
                try
                {
                    redis.StringSet(cacheKey,
                        _cachedIsSuperAdmin.ToString(),
                        TimeSpan.FromMinutes(SuperAdminCacheTtlMinutes));
                }
                catch { }
            }
        }
        catch
        {
            _cachedIsSuperAdmin = false;
        }
    }
}