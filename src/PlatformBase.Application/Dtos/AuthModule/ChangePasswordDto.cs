using System.ComponentModel.DataAnnotations;

namespace PlatformBase.Application.Dtos.AuthModule;

/// <summary>
/// 修改密码请求 DTO
/// </summary>
public class ChangePasswordDto
{
    [Required(ErrorMessage = "当前密码不能为空")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "新密码不能为空")]
    [MinLength(8, ErrorMessage = "密码长度至少8位")]
    public string NewPassword { get; set; } = string.Empty;
}
