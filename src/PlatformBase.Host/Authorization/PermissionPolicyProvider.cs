using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace PlatformBase.Host.Authorization;

/// <summary>
/// 动态权限策略提供程序 — 在运行时按需生成权限策略，无需预注册每个权限码
///
/// 实现逻辑：
///   1. 实现 IAuthorizationPolicyProvider，替换 ASP.NET Core 默认的策略提供程序
///   2. GetDefaultPolicyAsync / GetFallbackPolicyAsync → 委托给 DefaultAuthorizationPolicyProvider 处理
///   3. GetPolicyAsync 是核心方法，每次鉴权时被调用：
///      a. 判断策略名是否以 "Permission:" 开头
///      b. 若否 → 走默认逻辑（如 [Authorize] 无参数 → DefaultPolicy）
///      c. 若是 → 提取权限编码（去掉前缀），动态构建包含 JWT Bearer 认证 + PermissionRequirement 的策略
///   4. 这使得 [Permission("users.create")] 生效而无需在 Startup 中手动为每个权限调用 AddPolicy()
/// </summary>
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    /// <summary>
    /// ASP.NET Core 默认的策略提供程序
    /// 用于处理非权限策略（DefaultPolicy / FallbackPolicy / 已手动注册的策略）
    /// </summary>
    private readonly DefaultAuthorizationPolicyProvider _defaultPolicyProvider;

    /// <summary>
    /// 构造函数：接收全局 AuthorizationOptions 配置并创建默认策略提供程序
    /// </summary>
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _defaultPolicyProvider = new DefaultAuthorizationPolicyProvider(options); // 包装默认提供者（处理 [Authorize] 无参数等场景）
    }

    /// <inheritdoc />
    /// <summary>
    /// 获取默认授权策略（[Authorize] 无参数时使用）
    /// 直接委托给默认提供者，不涉及权限逻辑
    /// </summary>
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        => _defaultPolicyProvider.GetDefaultPolicyAsync(); // 返回默认策略（仅要求认证通过）

    /// <inheritdoc />
    /// <summary>
    /// 获取回退策略（全局注册 FallbackPolicy 时使用）
    /// 直接委托给默认提供者，不涉及权限逻辑
    /// </summary>
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        => _defaultPolicyProvider.GetFallbackPolicyAsync(); // 返回回退策略（通常为 null 或全局认证要求）

    /// <summary>
    /// 根据策略名动态创建授权策略
    /// 核心逻辑：拦截 "Permission:" 前缀的策略名，动态构建 PermissionRequirement
    /// </summary>
    /// <param name="policyName">策略名（来自 [Permission("code")] 中的 Policy 属性值）</param>
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // 非权限策略 → 走默认处理（如 [Authorize] 无参数传入的 DefaultPolicy）
        if (!policyName.StartsWith(PermissionAttribute.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
            return _defaultPolicyProvider.GetPolicyAsync(policyName); // 委托给默认提供者

        // 权限策略 → 提取权限编码并创建包含 PermissionRequirement 和 JWT Bearer 认证的策略
        // 去掉 "Permission:" 前缀，得到原始权限编码（如 "users.create"）
        var permissionCode = policyName[PermissionAttribute.PolicyPrefix.Length..];

        // 动态构建授权策略
        var policy = new AuthorizationPolicyBuilder()
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme) // 要求 JWT Bearer 认证
            .AddRequirements(new PermissionRequirement(permissionCode))        // 添加权限要求（含权限编码）
            .Build(); // 构建出 AuthorizationPolicy 对象

        return Task.FromResult<AuthorizationPolicy?>(policy); // 返回新建的策略
    }
}
