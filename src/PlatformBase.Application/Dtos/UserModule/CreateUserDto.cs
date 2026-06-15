using System.ComponentModel.DataAnnotations;
using PlatformBase.Core.Entities;

namespace PlatformBase.Application.Dtos.UserModule;

/// <summary>
/// 创建用户请求
/// 平台管理员必须指定 TenantId；租户管理员创建的用户自动归属当前租户
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

    /// <summary>
    /// 目标租户ID（平台管理员必填，租户管理员忽略）
    /// 平台管理员创建租户级用户时，必须指定已分配的租户ID
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// 用户类型（可选）
    /// 平台管理员可指定 TenantAdmin/TenantUser，默认 TenantUser
    /// 租户管理员只能创建 TenantUser
    /// </summary>
    public UserType? UserType { get; set; }

    /// <summary>初始角色 ID 列表（必填）</summary>
    [Required(ErrorMessage = "角色不能为空")]
    public List<Guid>? RoleIds { get; set; }

    /// <summary>初始部门 ID 列表（必填）</summary>
    [Required(ErrorMessage = "部门不能为空")]
    public List<Guid>? OrganizationUnitIds { get; set; }
}
