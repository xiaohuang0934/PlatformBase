namespace PlatformBase.Application.Dtos;

/// <summary>
/// 修改密码请求 DTO
/// 要求提供当前密码（验证身份）和新密码
/// </summary>
public class ChangePasswordDto
{
    /// <summary>当前密码，用于身份验证</summary>
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>新密码（需符合系统密码策略：8位以上、大小写字母+数字+特殊字符）</summary>
    public string NewPassword { get; set; } = string.Empty;
}
