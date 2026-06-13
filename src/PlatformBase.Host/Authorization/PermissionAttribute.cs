using Microsoft.AspNetCore.Authorization;              // 提供 AuthorizeAttribute 基类

namespace PlatformBase.Host.Authorization;

/// <summary>
/// 权限标记属性，用于在 Controller 或 Action 上声明所需的权限编码
///
/// 实现逻辑：
///   1. 继承 AuthorizeAttribute，ASP.NET Core 鉴权中间件自动识别并执行策略校验
///   2. 构造函数接收权限编码（如 "users.create"），将其包装为 "Permission:users.create" 策略名
///   3. PermissionPolicyProvider 的 GetPolicyAsync 检测到 "Permission:" 前缀后动态构建策略
///   4. 策略中包含 PermissionRequirement，由 PermissionAuthorizationHandler 执行实际鉴权逻辑
///   5. AllowMultiple=true → 支持同一 Action 上叠加多个权限属性（用户只需满足其一即可）
///   6. AttributeTargets.Class | Method → 可用于 Controller 类（全局设定默认权限）或具体 Action
///
/// <para>使用示例：</para>
/// <code>
/// [Permission("users.create")]  // 单个权限
/// [Permission("users.update")]  // 多个权限叠加（OR 逻辑）
/// public async Task&lt;IActionResult&gt; Create(CreateUserDto dto)
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)] // 可用于类/方法，支持多个实例叠加
public class PermissionAttribute : AuthorizeAttribute
{
    /// <summary>策略名称前缀，用于 PermissionPolicyProvider 识别权限策略</summary>
    internal const string PolicyPrefix = "Permission:";

    /// <summary>所需的权限编码（如 "users.create"）</summary>
    public string PermissionCode { get; }

    /// <summary>
    /// 声明一个权限要求
    /// 将权限编码包装为 "Permission:{permissionCode}" 策略名，
    /// PermissionPolicyProvider 在鉴权时会根据策略名动态生成 PermissionRequirement
    /// </summary>
    /// <param name="permissionCode">权限编码（如 users.create），对应 Permission 表中的 Code 字段</param>
    public PermissionAttribute(string permissionCode)
    {
        PermissionCode = permissionCode;                    // 保存原始权限编码（供 Handler 或其他代码使用）
        Policy = $"{PolicyPrefix}{permissionCode}";         // 设置 Policy 属性为 "Permission:users.create" 格式
    }
}
