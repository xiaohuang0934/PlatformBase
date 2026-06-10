namespace PlatformBase.Core.Models;

/// <summary>
/// 登录请求模型，用于 POST /api/auth/login
/// </summary>
public class LoginRequest
{
    /// <summary>用户名</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>密码（明文，传输时使用 HTTPS）</summary>
    public string Password { get; set; } = string.Empty;
}
