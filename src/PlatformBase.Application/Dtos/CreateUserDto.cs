namespace PlatformBase.Application.Dtos;

/// <summary>
/// 创建用户请求
/// </summary>
public class CreateUserDto
{
    /// <summary>登录用户名</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>登录密码</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>邮箱</summary>
    public string? Email { get; set; }

    /// <summary>手机号</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>初始角色 ID 列表</summary>
    public List<Guid>? RoleIds { get; set; }
}
