namespace PlatformBase.Core.Models;

/// <summary>
/// Token 刷新请求模型，POST /api/auth/refresh
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>RefreshToken 值（由 /api/auth/login 返回）</summary>
    public string RefreshToken { get; set; } = string.Empty;
}
