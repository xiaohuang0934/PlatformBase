using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace PlatformBase.Host.Authorization;

/// <summary>
/// 动态权限策略提供程序
/// 当检测到策略名以 "Permission:" 开头时，自动创建包含 <see cref="PermissionRequirement"/> 的授权策略
/// 这使得 <see cref="PermissionAttribute"/> 可以动态生成策略，无需手动预注册每个权限码
/// </summary>
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _defaultPolicyProvider;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _defaultPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    /// <inheritdoc />
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        => _defaultPolicyProvider.GetDefaultPolicyAsync();

    /// <inheritdoc />
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        => _defaultPolicyProvider.GetFallbackPolicyAsync();

    /// <summary>
    /// 根据策略名动态创建授权策略
    /// </summary>
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // 非权限策略 → 走默认处理
        if (!policyName.StartsWith(PermissionAttribute.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
            return _defaultPolicyProvider.GetPolicyAsync(policyName);

        // 权限策略 → 提取权限编码并创建包含 PermissionRequirement 和 JWT Bearer 认证的策略
        var permissionCode = policyName[PermissionAttribute.PolicyPrefix.Length..];

        var policy = new AuthorizationPolicyBuilder()
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .AddRequirements(new PermissionRequirement(permissionCode))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
