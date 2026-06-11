namespace PlatformBase.Core.Models;

/// <summary>
/// SMTP 邮件配置
/// </summary>
public class SmtpConfig
{
    /// <summary>SMTP 服务器地址</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>端口，默认 587</summary>
    public int Port { get; set; } = 587;

    /// <summary>发件邮箱账号</summary>
    public string User { get; set; } = string.Empty;

    /// <summary>授权码（非登录密码）</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>发件人显示地址，默认同 User</summary>
    public string? From { get; set; }
}
