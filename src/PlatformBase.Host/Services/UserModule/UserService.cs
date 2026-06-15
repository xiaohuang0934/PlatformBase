using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PlatformBase.Application.Services.OrganizationModule;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Extensions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Repositories;
using PlatformBase.Core.Services;
using PlatformBase.Host.IdentityServer;
using PlatformBase.Infrastructure.Data;
using StackExchange.Redis;
using Role = PlatformBase.Core.Entities.Role;

namespace PlatformBase.Host.Services.UserModule;

/// <summary>
/// 用户管理服务实现
/// 通过 <see cref="IUnitOfWork"/> 操作 User / Role 等 BaseEntity 实体（含审计追踪）
/// 关联表（UserRole）通过 <see cref="AppDbContext"/> 直接查询
/// 密码使用 BCrypt 哈希，支持登录失败追踪与账户锁定
/// </summary>
public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _context;
    private readonly IDatabase? _redis;
    private readonly PersistedGrantStore _grantStore;
    private readonly ICurrentUserContext _currentUser;
    private readonly ISystemParamService _sysParam;

    private const int DefaultMaxFailedAttempts = 5;
    private const int DefaultLockoutMinutes = 5;

    public UserService(IUnitOfWork uow, AppDbContext context, IServiceProvider serviceProvider,
        PersistedGrantStore grantStore, ICurrentUserContext currentUser,
        ISystemParamService sysParam)
    {
        _uow = uow;
        _context = context;
        _redis = serviceProvider.GetService<IConnectionMultiplexer>()?.GetDatabase();
        _grantStore = grantStore;
        _currentUser = currentUser;
        _sysParam = sysParam;
    }

    /// <inheritdoc />
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _uow.Repository<User>().GetByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = StringExtensions.Normalize(username);
        return await _uow.Repository<User>()
            .FirstOrDefaultAsync(u => u.NormalizedUsername == normalized, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        if (await _uow.Repository<User>().AnyAsync(
                u => u.NormalizedUsername == StringExtensions.Normalize(user.Username), cancellationToken))
        {
            throw new BusinessException($"用户名 '{user.Username}' 已存在", ErrorCode.DuplicateRecord);
        }

        user.NormalizedUsername = StringExtensions.Normalize(user.Username);
        user.NormalizedEmail = user.Email != null ? StringExtensions.Normalize(user.Email) : null;

        var created = await _uow.Repository<User>().AddAsync(user, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return created;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        // 检测 TenantId 变更 → 失效租户缓存
        var existing = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == user.Id)
            .Select(u => new { u.TenantId })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing != null && existing.TenantId != user.TenantId)
            _currentUser.InvalidateTenantCache();

        user.NormalizedUsername = StringExtensions.Normalize(user.Username);
        user.NormalizedEmail = user.Email != null ? StringExtensions.Normalize(user.Email) : null;

        _uow.Repository<User>().Update(user);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var roleIds = await _context.Set<UserRole>()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);

        if (roleIds.Count == 0) return [];

        var roles = await _uow.Repository<Role>()
            .FindAsync(r => roleIds.Contains(r.Id), cancellationToken);

        return roles.Select(r => r.Name).ToList();
    }

    /// <inheritdoc />
    public async Task AddToRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var exists = await _context.Set<UserRole>()
            .AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken);

        if (exists) return;

        // 校验角色是否存在
        var roleExists = await _uow.Repository<Role>()
            .AnyAsync(r => r.Id == roleId, cancellationToken);
        if (!roleExists)
            throw new BusinessException("角色不存在", ErrorCode.DataNotFound);

        _context.Set<UserRole>().Add(new UserRole { UserId = userId, RoleId = roleId });
        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateUserPermsCacheAsync(userId);
    }

    /// <inheritdoc />
    public async Task RemoveFromRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var userRole = await _context.Set<UserRole>()
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken);

        if (userRole != null)
        {
            _context.Set<UserRole>().Remove(userRole);
            await _context.SaveChangesAsync(cancellationToken);
            await InvalidateUserPermsCacheAsync(userId);
        }
    }

    /// <inheritdoc />
    public async Task ClearRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userRoles = await _context.Set<UserRole>()
            .Where(ur => ur.UserId == userId)
            .ToListAsync(cancellationToken);

        _context.Set<UserRole>().RemoveRange(userRoles);
        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateUserPermsCacheAsync(userId);
    }

    /// <inheritdoc />
    public async Task UpdatePasswordAsync(Guid userId, string passwordHash, string securityStamp,
        CancellationToken cancellationToken = default)
    {
        var user = await GetByIdAsync(userId, cancellationToken);
        if (user == null)
            throw new BusinessException("用户不存在", ErrorCode.UserNotFound);

        user.PasswordHash = passwordHash;
        user.SecurityStamp = securityStamp;
        _uow.Repository<User>().Update(user);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> CheckPasswordAsync(Guid userId, string password,
        CancellationToken cancellationToken = default)
    {
        var user = await GetByIdAsync(userId, cancellationToken);
        if (user == null)
            throw new BusinessException("用户不存在", ErrorCode.UserNotFound);

        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
    }

    /// <inheritdoc />
    public async Task RecordLoginSuccessAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetByIdAsync(userId, cancellationToken);
        if (user == null) return;

        if (user.AccessFailedCount > 0 || user.LockoutEnd.HasValue)
        {
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            _uow.Repository<User>().Update(user);
            await _uow.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task RecordLoginFailedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetByIdAsync(userId, cancellationToken);
        if (user == null) return;

        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
            return;

        user.AccessFailedCount++;

        var maxAttempts = await _sysParam.GetValueAsync("max_login_attempts", DefaultMaxFailedAttempts, cancellationToken);
        if (user.AccessFailedCount >= maxAttempts)
        {
            var lockMins = await _sysParam.GetValueAsync("lockout_minutes", DefaultLockoutMinutes, cancellationToken);
            user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(lockMins);
        }

        _uow.Repository<User>().Update(user);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    private static string Normalize(string value) => (value ?? string.Empty).ToUpperInvariant();

    // ═══════════════════ 用户管理扩展（v1.1） ═══════════════════

    /// <inheritdoc />
    public async Task<PagedResult<UserDto>> GetPagedAsync(UserQuery query, CancellationToken ct = default)
    {
        var kw = query.Keyword?.Trim().ToUpperInvariant();

        // 计算生效的租户列表
        IReadOnlyList<Guid> effectiveTenantIds = ResolveEffectiveTenantIds(query.TenantIds);

        var filter = ((Expression<Func<User, bool>>?)null)
            .AppendIf(effectiveTenantIds.Count > 0 && _currentUser.UserType == UserType.PlatformAdmin,
                u => u.TenantId == null || effectiveTenantIds.Contains(u.TenantId.Value))
            .AppendIf(effectiveTenantIds.Count > 0 && _currentUser.UserType != UserType.PlatformAdmin,
                u => u.TenantId == effectiveTenantIds.FirstOrDefault())
            .AppendIf(effectiveTenantIds.Count == 0 && _currentUser.UserType != UserType.PlatformAdmin,
                u => false)
            .AppendIf(effectiveTenantIds.Count == 0 && _currentUser.UserType == UserType.PlatformAdmin,
                u => u.TenantId == null)
            .AppendIf(query.IsActive.HasValue,
                u => u.IsActive == query.IsActive.Value)
            .AppendIf(!string.IsNullOrWhiteSpace(kw),
                u => u.NormalizedUsername.Contains(kw!) || (u.NormalizedEmail != null && u.NormalizedEmail.Contains(kw!)));

        var result = await _uow.Repository<User>().GetPagedAsync(new PagedRequest
        {
            PageIndex = query.PageIndex, PageSize = query.PageSize,
            SortField = query.SortField ?? nameof(User.Username), IsAscending = query.IsAscending
        }, filter, ct);

        var userRoles = await _context.Set<UserRole>()
            .Where(ur => result.Items.Select(u => u.Id).Contains(ur.UserId))
            .ToListAsync(ct);

        var roleIds = userRoles.Select(ur => ur.RoleId).Distinct().ToList();
        var roles = roleIds.Count > 0
            ? await _uow.Repository<Role>().FindAsync(r => roleIds.Contains(r.Id), ct)
            : [];

        var roleMap = roles.ToDictionary(r => r.Id, r => r.Name);
        var userRoleMap = userRoles.GroupBy(ur => ur.UserId)
            .ToDictionary(g => g.Key, g => g.Select(ur => roleMap.GetValueOrDefault(ur.RoleId, "")).ToList());

        // 批量查询用户-部门关联
        var userIds = result.Items.Select(u => u.Id).ToList();
        var userOrgLinks = await _context.Set<UserOrganizationUnit>()
            .Where(uo => userIds.Contains(uo.UserId))
            .ToListAsync(ct);

        var orgIds = userOrgLinks.Select(uo => uo.OrganizationUnitId).Distinct().ToList();
        var orgs = orgIds.Count > 0
            ? await _uow.Repository<OrganizationUnit>().FindAsync(o => orgIds.Contains(o.Id), ct)
            : [];
        var orgMap = orgs.ToDictionary(o => o.Id, o => new OrgUnitNode
        {
            Id = o.Id, Name = o.Name, Code = o.Code, ParentId = o.ParentId, SortOrder = o.SortOrder
        });
        var userOrgMap = userOrgLinks.GroupBy(uo => uo.UserId)
            .ToDictionary(g => g.Key, g => g.Select(uo => orgMap.GetValueOrDefault(uo.OrganizationUnitId)).Where(n => n != null).ToList()!);

        // 批量查询租户名称
        var tenantIds = result.Items.Select(u => u.TenantId).Where(tid => tid.HasValue).Select(tid => tid!.Value).Distinct().ToList();
        var tenants = tenantIds.Count > 0
            ? await _context.Set<Tenant>().AsNoTracking().Where(t => tenantIds.Contains(t.Id)).ToListAsync(ct)
            : [];
        var tenantNameMap = tenants.ToDictionary(t => t.Id, t => t.Name);

        var dtos = result.Items.Select(u =>
        {
            var roleNames = userRoleMap.GetValueOrDefault(u.Id, []);
            var orgNodes = userOrgMap.GetValueOrDefault(u.Id, []) as IReadOnlyList<OrgUnitNode>;
            return new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                EmailConfirmed = u.EmailConfirmed,
                PhoneNumber = u.PhoneNumber,
                IsActive = u.IsActive,
                UserType = u.UserType,
                Roles = roleNames,
                OrganizationUnits = orgNodes,
                TenantName = u.TenantId.HasValue ? tenantNameMap.GetValueOrDefault(u.TenantId.Value) : null,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            };
        }).ToList();

        return new PagedResult<UserDto>(result.TotalCount, result.PageIndex, result.PageSize, dtos);
    }

    /// <summary>
    /// 解析生效的租户列表：
    /// - 平台用户传了 tenantIds → 校验后返回
    /// - 否则 → 返回 CurrentTenantIds
    /// </summary>
    private IReadOnlyList<Guid> ResolveEffectiveTenantIds(List<Guid>? queryTenantIds)
    {
        // 租户用户：直接返回 CurrentTenantIds（只有一个租户）
        if (_currentUser.UserType != UserType.PlatformAdmin)
            return _currentUser.CurrentTenantIds;

        // 平台用户：未传 tenantIds → 返回 CurrentTenantIds
        if (queryTenantIds == null || queryTenantIds.Count == 0)
            return _currentUser.CurrentTenantIds;

        // 平台用户：传了 tenantIds → 校验是否在 TenantIds 范围内
        var invalid = queryTenantIds.Where(t => !_currentUser.TenantIds.Contains(t)).ToList();
        if (invalid.Count > 0)
            throw new BusinessException($"无权访问租户: {invalid[0]}", ErrorCode.Forbidden);

        return queryTenantIds;
    }

    /// <inheritdoc />
    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var user = await GetByIdAsync(id, ct);
        if (user == null)
            throw new BusinessException("用户不存在", ErrorCode.UserNotFound);

        user.IsActive = isActive;
        _uow.Repository<User>().Update(user);
        await _uow.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await GetByIdAsync(id, ct);
        if (user == null) return;

        // 清理关联记录
        var userRoles = await _context.Set<UserRole>()
            .Where(ur => ur.UserId == id).ToListAsync(ct);
        _context.Set<UserRole>().RemoveRange(userRoles);

        var userPerms = await _context.Set<UserPermission>()
            .Where(up => up.UserId == id).ToListAsync(ct);
        _context.Set<UserPermission>().RemoveRange(userPerms);

        _uow.Repository<User>().SoftDelete(user);
        await _uow.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken ct = default)
    {
        var user = await GetByIdAsync(id, ct);
        if (user == null)
            throw new BusinessException("用户不存在", ErrorCode.UserNotFound);

        var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        var newStamp = Guid.NewGuid().ToString();

        await _uow.BeginTransactionAsync(ct);
        try
        {
            user.PasswordHash = newHash;
            user.SecurityStamp = newStamp;
            _uow.Repository<User>().Update(user);
            await _uow.SaveChangesAsync(ct);

            await _grantStore.RevokeUserTokensAsync(id.ToString());
            await _uow.CommitTransactionAsync(ct);
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }

        await InvalidateStampCacheAsync(id);
    }

    private async Task InvalidateUserPermsCacheAsync(Guid userId)
    {
        if (_redis == null) return;
        try { await _redis.KeyDeleteAsync($"user:perms:{userId}"); }
        catch { /* Redis 不可用，降级跳过 */ }
    }

    private async Task InvalidateStampCacheAsync(Guid userId)
    {
        if (_redis == null) return;
        try { await _redis.KeyDeleteAsync($"stamp:{userId}"); }
        catch { /* Redis 不可用，降级跳过 */ }
    }

    // ═══════════════════ 用户-组织架构关联管理 ═══════════════════

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrganizationUnit>> GetUserOrganizationsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var orgIds = await _context.Set<UserOrganizationUnit>()
            .Where(uo => uo.UserId == userId)
            .Select(uo => uo.OrganizationUnitId)
            .ToListAsync(cancellationToken);

        if (orgIds.Count == 0) return [];

        var orgs = await _uow.Repository<OrganizationUnit>()
            .FindAsync(o => orgIds.Contains(o.Id), cancellationToken);

        return orgs;
    }

    /// <inheritdoc />
    public async Task AddToOrganizationAsync(Guid userId, Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var exists = await _context.Set<UserOrganizationUnit>()
            .AnyAsync(uo => uo.UserId == userId && uo.OrganizationUnitId == organizationUnitId, cancellationToken);

        if (exists) return;

        var orgExists = await _uow.Repository<OrganizationUnit>()
            .AnyAsync(o => o.Id == organizationUnitId, cancellationToken);
        if (!orgExists)
            throw new BusinessException("部门不存在", ErrorCode.DataNotFound);

        _context.Set<UserOrganizationUnit>().Add(new UserOrganizationUnit
        {
            UserId = userId,
            OrganizationUnitId = organizationUnitId
        });
        await _context.SaveChangesAsync(cancellationToken);

        _currentUser.InvalidateOrganizationCache();
    }

    /// <inheritdoc />
    public async Task RemoveFromOrganizationAsync(Guid userId, Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var userOrg = await _context.Set<UserOrganizationUnit>()
            .FirstOrDefaultAsync(uo => uo.UserId == userId && uo.OrganizationUnitId == organizationUnitId, cancellationToken);

        if (userOrg != null)
        {
            _context.Set<UserOrganizationUnit>().Remove(userOrg);
            await _context.SaveChangesAsync(cancellationToken);
            _currentUser.InvalidateOrganizationCache();
        }
    }

    /// <inheritdoc />
    public async Task SetOrganizationsAsync(Guid userId, IReadOnlyList<Guid> organizationUnitIds, CancellationToken cancellationToken = default)
    {
        var user = await GetByIdAsync(userId, cancellationToken);
        if (user == null)
            throw new BusinessException("用户不存在", ErrorCode.UserNotFound);

        // 校验部门是否存在且属于用户所在租户
        if (organizationUnitIds.Count > 0)
        {
            var validOrgs = await _uow.Repository<OrganizationUnit>()
                .FindAsync(o => organizationUnitIds.Contains(o.Id), cancellationToken);

            if (validOrgs.Count != organizationUnitIds.Count)
                throw new BusinessException("部分部门不存在", ErrorCode.DataNotFound);

            var userTenantId = user.TenantId;
            var invalidOrgs = validOrgs.Where(o => o.TenantId != userTenantId).ToList();
            if (invalidOrgs.Count > 0)
                throw new BusinessException("部门不属于用户所在租户", ErrorCode.OrganizationNotInTenant);
        }

        // 移除现有关联
        var existingOrgs = await _context.Set<UserOrganizationUnit>()
            .Where(uo => uo.UserId == userId)
            .ToListAsync(cancellationToken);
        _context.Set<UserOrganizationUnit>().RemoveRange(existingOrgs);

        // 添加新关联
        foreach (var orgId in organizationUnitIds)
        {
            _context.Set<UserOrganizationUnit>().Add(new UserOrganizationUnit
            {
                UserId = userId,
                OrganizationUnitId = orgId
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        _currentUser.InvalidateOrganizationCache();
    }
}
