namespace PlatformBase.Application.Dtos.UserModule;

/// <summary>
/// 更新用户请求
/// </summary>
public class UpdateUserDto
{
    /// <summary>邮箱</summary>
    public string? Email { get; set; }

    /// <summary>手机号</summary>
    public string? PhoneNumber { get; set; }
}
