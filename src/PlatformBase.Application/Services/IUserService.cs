using PlatformBase.Application.Dtos;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Models;

namespace PlatformBase.Application.Services;

/// <summary>
/// 用户管理服务接口，提供用户 CRUD、角色管理、密码验证、登录追踪等核心操作
/// </summary>
public interface IUserService
{
    /// <summary>根据用户 ID 查询用户（不包含已软删除的用户，由全局过滤器自动排除）</summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>根据用户名（大小写不敏感）查询用户</summary>
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>创建新用户，返回入库后的用户实体</summary>
    Task<User> CreateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>更新用户信息</summary>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>查询用户拥有的角色名称列表</summary>
    Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>给用户赋予某个角色</summary>
    Task AddToRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>移除用户的一个角色</summary>
    Task RemoveFromRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>清除用户所有角色</summary>
    Task ClearRolesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>更新用户密码和关联的安全戳</summary>
    Task UpdatePasswordAsync(Guid userId, string passwordHash, string securityStamp,
        CancellationToken cancellationToken = default);

    /// <summary>验证用户密码是否正确</summary>
    Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken cancellationToken = default);

    /// <summary>记录登录成功：清零 AccessFailedCount</summary>
    Task RecordLoginSuccessAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>记录登录失败：递增 AccessFailedCount，达到阈值时锁定账户</summary>
    Task RecordLoginFailedAsync(Guid userId, CancellationToken cancellationToken = default);

    // ───── 用户管理扩展（v1.1）─────

    /// <summary>分页查询用户列表</summary>
    Task<PagedResult<UserDto>> GetPagedAsync(UserQuery query, CancellationToken cancellationToken = default);

    /// <summary>启用/禁用用户</summary>
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>软删除用户</summary>
    Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>重置用户密码（管理员操作）</summary>
    Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken = default);
}
