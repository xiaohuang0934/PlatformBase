using PlatformBase.Core.Entities;

namespace PlatformBase.Application.Services.AuthorizationModule;

/// <summary>
/// 数据权限授权服务接口
/// 集中处理用户类型校验、租户覆盖、部门/角色归属验证
/// </summary>
public interface IDataScopeAuthorizationService
{
    /// <summary>
    /// 授权当前用户操作指定租户和部门
    /// - 检查用户类型是否在禁止列表中，在则返回 IsBanned=true
    /// - 低权限用户自动覆盖租户ID（强制使用自己的租户）
    /// - 校验部门是否属于目标租户
    /// - 校验角色是全局角色或属于目标租户
    /// </summary>
    /// <param name="bannedUserTypes">禁止操作的用户类型列表</param>
    /// <param name="requestTenantId">请求中的租户ID（高权限用户使用，低权限用户忽略）</param>
    /// <param name="requestUserType">请求中的用户类型（仅平台管理员可指定）</param>
    /// <param name="requestOrgIds">请求中的部门ID列表</param>
    /// <param name="requestRoleIds">请求中的角色ID列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<DataScopeAuthResult> AuthorizeAsync(
        IReadOnlyList<UserType> bannedUserTypes,
        Guid? requestTenantId,
        UserType? requestUserType,
        List<Guid>? requestOrgIds,
        List<Guid>? requestRoleIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 数据权限授权结果
/// </summary>
public class DataScopeAuthResult
{
    /// <summary>当前用户是否被禁止操作</summary>
    public bool IsBanned { get; set; }

    /// <summary>目标租户ID（已根据用户层级覆盖）</summary>
    public Guid? TenantId { get; set; }

    /// <summary>目标用户类型（已根据用户层级覆盖）</summary>
    public UserType UserType { get; set; } = UserType.TenantUser;

    /// <summary>禁止原因（IsBanned=true 时填充）</summary>
    public string? BanReason { get; set; }
}