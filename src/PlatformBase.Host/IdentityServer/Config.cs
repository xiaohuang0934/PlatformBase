using IdentityServer4;
using IdentityServer4.Models;

namespace PlatformBase.Host.IdentityServer;

/// <summary>
/// IdentityServer4 静态配置
/// 定义 OAuth2/OIDC 的客户端、API 资源、身份资源和授权范围
/// 所有配置采用代码化管理，便于版本控制和审计追踪
/// </summary>
public static class Config
{
    /// <summary>
    /// API 授权范围定义
    /// Scope 是客户端可以请求的"最小访问单元"
    /// </summary>
    public static IEnumerable<ApiScope> ApiScopes =>
    [
        new("api1", "PlatformBase API — 全部接口访问权限")
    ];

    /// <summary>
    /// API 资源定义
    /// 每个 ApiResource 可以包含多个 ApiScope
    /// </summary>
    public static IEnumerable<ApiResource> ApiResources =>
    [
        new("api1", "PlatformBase API")
        {
            Scopes = { "api1" },
            UserClaims = { "security_stamp", "user_type", "super_admin" }
        }
    ];

    /// <summary>
    /// 身份资源定义
    /// 控制 JWT Token 中包含的用户基本信息字段
    /// </summary>
    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),   // subject (sub) 声明
        new IdentityResources.Profile(), // name, family_name, website 等
        new IdentityResources.Email()    // email, email_verified
    ];

    /// <summary>
    /// 客户端定义
    /// 每个 Client 代表一个可以请求令牌的应用程序（SPA、移动端、Swagger 等）
    /// </summary>
    public static IEnumerable<Client> Clients =>
    [
        // ── 默认前端/API 客户端 ──
        new()
        {
            ClientId = "platformbase-client",
            ClientName = "PlatformBase 默认客户端",
            ClientSecrets = { new Secret("platformbase-secret".Sha256()) },

            // Password + Refresh Token 组合授权
            AllowedGrantTypes = { GrantType.ResourceOwnerPassword, "refresh_token" },

            AllowedScopes =
            {
                "api1", "openid", "profile", "email",
                IdentityServerConstants.StandardScopes.OfflineAccess
            },

            AccessTokenLifetime = 300,            // AT 5 分钟（短 Token 降低泄漏风险）
            AllowOfflineAccess = true,            // 允许获取 Refresh Token
            RefreshTokenUsage = TokenUsage.OneTimeOnly, // RT 一次性使用（级联更新）
            RefreshTokenExpiration = TokenExpiration.Sliding, // 滑动过期（持续使用不过期）
            AbsoluteRefreshTokenLifetime = 2592000, // 绝对有效期 30 天
            SlidingRefreshTokenLifetime = 604800,    // 每次刷新延长 7 天
            UpdateAccessTokenClaimsOnRefresh = true   // 刷新时更新 Claims（安全戳变更即时生效）
        },

        // ── Swagger 测试客户端 ──
        new()
        {
            ClientId = "platformbase-swagger",
            ClientName = "Swagger UI 测试客户端",
            ClientSecrets = { new Secret("swagger-secret".Sha256()) },

            AllowedGrantTypes = { GrantType.ResourceOwnerPassword, "refresh_token" },
            AllowAccessTokensViaBrowser = true,

            AllowedScopes =
            {
                "api1", "openid", "profile", "email",
                IdentityServerConstants.StandardScopes.OfflineAccess
            },

            AccessTokenLifetime = 300,
            AllowOfflineAccess = true,
            RefreshTokenUsage = TokenUsage.OneTimeOnly,
            RefreshTokenExpiration = TokenExpiration.Absolute,
            AbsoluteRefreshTokenLifetime = 2592000
        }
    ];
}
