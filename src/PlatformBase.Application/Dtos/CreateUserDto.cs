using System.ComponentModel.DataAnnotations;

namespace PlatformBase.Application.Dtos;

/// <summary>
/// 创建用户请求
/// </summary>
public class CreateUserDto
{
    /// <summary>登录用户名</summary>
    [Required(ErrorMessage = "用户名不能为空")]
    public string Username { get; set; } = string.Empty;

    /// <summary>登录密码</summary>
    [Required(ErrorMessage = "密码不能为空")]
    [MinLength(8, ErrorMessage = "密码长度至少8位")]
    public string Password { get; set; } = string.Empty;

    /// <summary>邮箱</summary>
    public string? Email { get; set; }

    /// <summary>手机号</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>初始角色 ID 列表</summary>
    public List<Guid>? RoleIds { get; set; }
}
