using PlatformBase.Core.Entities;
using PlatformBase.Core.Models;

namespace PlatformBase.Application.Services.UserModule;

/// <summary>
/// 用户管理服务接口，提供用户 CRUD、角色管理、密码验证、登录追踪等核心操作
/// </summary>
public interface IUserService
{
    /// <summary>根据用户 ID 查询用户（不包含已软删除的用户，由全局过滤器自动排除）</summary>
    /// <param name="id">用户 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户实体，未找到返回 null</returns>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>根据用户名（大小写不敏感）查询用户</summary>
    /// <param name="username">用户名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户实体，未找到返回 null</returns>
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>创建新用户</summary>
    /// <param name="user">用户实体（需包含基本信息和 BCrypt 密码哈希）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>入库后的用户实体</returns>
    Task<User> CreateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>更新用户信息</summary>
    /// <param name="user">用户实体（已修改）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>查询用户拥有的角色名称列表</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色名称列表（如 ["Admin", "User"]）</returns>
    Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>给用户赋予某个角色</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="roleId">角色 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddToRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>移除用户的一个角色</summary>
    Task RemoveFromRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>清除用户所有角色</summary>
    Task ClearRolesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>更新用户密码哈希和安全戳</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="passwordHash">BCrypt 格式密码哈希</param>
    /// <param name="securityStamp">新安全戳（用于使旧 Token 失效）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdatePasswordAsync(Guid userId, string passwordHash, string securityStamp,
        CancellationToken cancellationToken = default);

    /// <summary>验证用户密码是否正确</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="password">明文密码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>密码是否匹配</returns>
    Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken cancellationToken = default);

    /// <summary>记录登录成功，清零 AccessFailedCount 并解锁</summary>
    Task RecordLoginSuccessAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>记录登录失败，递增 AccessFailedCount，达到阈值时锁定账户</summary>
    Task RecordLoginFailedAsync(Guid userId, CancellationToken cancellationToken = default);

    // ───── 用户管理扩展（v1.1）─────

    /// <summary>分页查询用户列表，支持关键词搜索和状态筛选</summary>
    /// <param name="query">查询条件（关键词、是否启用、分页参数）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果（含用户角色）</returns>
    Task<PagedResult<UserDto>> GetPagedAsync(UserQuery query, CancellationToken cancellationToken = default);

    /// <summary>启用/禁用用户</summary>
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>软删除用户，同时清理关联的 UserRole 和 UserPermission</summary>
    Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>管理员重置用户密码，同时撤销旧 RefreshToken 并失效 Stamp 缓存</summary>
    Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken = default);
}
