using PlatformBase.Core.Entities;

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

    /// <summary>
    /// 用户类型（可选，仅平台管理员可修改）
    /// 租户管理员修改用户类型会被忽略
    /// </summary>
    public UserType? UserType { get; set; }

    /// <summary>部门 ID 列表（全量替换）</summary>
    public List<Guid>? OrganizationUnitIds { get; set; }
}
