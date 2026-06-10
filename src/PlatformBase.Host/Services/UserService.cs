using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PlatformBase.Application.Dtos;
using PlatformBase.Application.Services;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Repositories;
using PlatformBase.Infrastructure.Data;

namespace PlatformBase.Host.Services;

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

    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 5;

    public UserService(IUnitOfWork uow, AppDbContext context)
    {
        _uow = uow;
        _context = context;
    }

    /// <inheritdoc />
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _uow.Repository<User>().GetByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(username);
        return await _uow.Repository<User>()
            .FirstOrDefaultAsync(u => u.NormalizedUsername == normalized, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        if (await _uow.Repository<User>().AnyAsync(
                u => u.NormalizedUsername == Normalize(user.Username), cancellationToken))
        {
            throw new BusinessException($"用户名 '{user.Username}' 已存在", ErrorCode.DuplicateRecord);
        }

        user.NormalizedUsername = Normalize(user.Username);
        user.NormalizedEmail = user.Email != null ? Normalize(user.Email) : null;

        var created = await _uow.Repository<User>().AddAsync(user, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return created;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        user.NormalizedUsername = Normalize(user.Username);
        user.NormalizedEmail = user.Email != null ? Normalize(user.Email) : null;

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

        _context.Set<UserRole>().Add(new UserRole { UserId = userId, RoleId = roleId });
        await _context.SaveChangesAsync(cancellationToken);
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

        if (user.AccessFailedCount >= MaxFailedAttempts)
            user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(LockoutMinutes);

        _uow.Repository<User>().Update(user);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    private static string Normalize(string value) => (value ?? string.Empty).ToUpperInvariant();

    // ═══════════════════ 用户管理扩展（v1.1） ═══════════════════

    /// <inheritdoc />
    public async Task<PagedResult<UserDto>> GetPagedAsync(UserQuery query, CancellationToken ct = default)
    {
        Expression<Func<User, bool>>? filter = null;

        if (query.IsActive.HasValue)
        {
            var active = query.IsActive.Value;
            filter = u => u.IsActive == active;
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim().ToUpperInvariant();
            Expression<Func<User, bool>> kwFilter = u =>
                u.NormalizedUsername.Contains(kw) || (u.NormalizedEmail != null && u.NormalizedEmail.Contains(kw));

            filter = filter == null ? kwFilter : CombineAnd(filter, kwFilter);
        }

        var request = new PagedRequest
        {
            PageIndex = query.PageIndex,
            PageSize = query.PageSize,
            SortField = query.SortField ?? nameof(User.Username),
            IsAscending = query.IsAscending
        };

        var result = await _uow.Repository<User>().GetPagedAsync(request, filter, ct);

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

        var dtos = result.Items.Select(u =>
        {
            var roleNames = userRoleMap.GetValueOrDefault(u.Id, []);
            return new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                EmailConfirmed = u.EmailConfirmed,
                PhoneNumber = u.PhoneNumber,
                IsActive = u.IsActive,
                Roles = roleNames,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            };
        }).ToList();

        return new PagedResult<UserDto>(result.TotalCount, result.PageIndex, result.PageSize, dtos);
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

        user.PasswordHash = newHash;
        user.SecurityStamp = newStamp;
        _uow.Repository<User>().Update(user);
        await _uow.SaveChangesAsync(ct);
    }

    private static Expression<Func<T, bool>> CombineAnd<T>(
        Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
    {
        var param = Expression.Parameter(typeof(T));
        var body = Expression.AndAlso(
            Expression.Invoke(left, param),
            Expression.Invoke(right, param));
        return Expression.Lambda<Func<T, bool>>(body, param);
    }
}
