using PlatformBase.Core.Models;

namespace PlatformBase.Application.Services.AuthModule;

/// <summary>
/// 认证授权服务接口，封装登录、Token 刷新、密码修改等完整认证流程
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 用户登录：验证用户名密码 → 检查账户状态 → 生成 AccessToken + RefreshToken
    /// 失败时抛出 <see cref="Core.Exceptions.BusinessException"/>
    /// </summary>
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刷新 Token：验证 RefreshToken 有效性 → 消费旧 RT → 签发新 AT + RT
    /// 失败时抛出 <see cref="Core.Exceptions.BusinessException"/>
    /// </summary>
    Task<LoginResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// 修改密码：验证当前密码 → BCrypt 哈希新密码 → 更新安全戳 → 撤销所有 RefreshToken
    /// 失败时抛出 <see cref="Core.Exceptions.BusinessException"/>
    /// </summary>
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword,
        CancellationToken cancellationToken = default);
}
