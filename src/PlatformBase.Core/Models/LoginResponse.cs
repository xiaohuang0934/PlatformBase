namespace PlatformBase.Core.Models;

/// <summary>
/// 登录响应模型，返回 JWT Token 及其元信息
/// </summary>
public class LoginResponse
{
    /// <summary>JWT Access Token，后续请求通过 Authorization: Bearer {token} 携带</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>令牌类型，固定为 "Bearer"</summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>Token 有效期（秒），默认 300（5 分钟）</summary>
    public int ExpiresIn { get; set; }

    /// <summary>Refresh Token，用于在 Access Token 过期后无感刷新，30 天有效</summary>
    public string? RefreshToken { get; set; }
}
