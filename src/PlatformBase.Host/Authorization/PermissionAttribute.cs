using Microsoft.AspNetCore.Authorization;

namespace PlatformBase.Host.Authorization;

/// <summary>
/// 权限标记属性，用于在 Controller 或 Action 上声明所需的权限编码
/// <para>使用示例：</para>
/// <code>
/// [Permission("users.create")]
/// public async Task&lt;IActionResult&gt; Create(CreateUserDto dto)
/// </code>
/// <para>多个权限时可以使用多个属性，用户只需满足其中之一即可</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class PermissionAttribute : AuthorizeAttribute
{
    /// <summary>策略名称前缀</summary>
    internal const string PolicyPrefix = "Permission:";

    /// <summary>所需的权限编码</summary>
    public string PermissionCode { get; }

    /// <summary>
    /// 声明一个权限要求
    /// </summary>
    /// <param name="permissionCode">权限编码（如 users.create）</param>
    public PermissionAttribute(string permissionCode)
    {
        PermissionCode = permissionCode;
        Policy = $"{PolicyPrefix}{permissionCode}";
    }
}
