using System.ComponentModel.DataAnnotations;

namespace PlatformBase.Application.Dtos.UserModule;

/// <summary>
/// 重置密码请求体
/// </summary>
public class ResetPasswordRequest
{
    [Required(ErrorMessage = "新密码不能为空")]
    [MinLength(8, ErrorMessage = "密码长度至少8位")]
    public string NewPassword { get; set; } = string.Empty;
}
