using Microsoft.AspNetCore.Authorization;

namespace PlatformBase.Host.Authorization;

/// <summary>
/// 权限授权要求，携带具体的权限编码（如：users.create）
/// 由 <see cref="PermissionPolicyProvider"/> 动态创建，传递给 <see cref="PermissionAuthorizationHandler"/>
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>要求的权限编码</summary>
    public string PermissionCode { get; }

    public PermissionRequirement(string permissionCode)
    {
        PermissionCode = permissionCode;
    }
}
