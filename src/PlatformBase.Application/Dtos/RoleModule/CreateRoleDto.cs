using System.ComponentModel.DataAnnotations;

namespace PlatformBase.Application.Dtos.RoleModule;

/// <summary>
/// 创建角色请求
/// 平台管理员可创建全局角色（TenantId=null）或租户级角色（TenantId指定）
/// 租户管理员只能创建租户级角色（TenantId自动为当前租户）
/// </summary>
public class CreateRoleDto
{
    /// <summary>角色名称（全局唯一）</summary>
    [Required(ErrorMessage = "角色名称不能为空")]
    public string Name { get; set; } = string.Empty;

    /// <summary>角色编码（全局唯一）</summary>
    [Required(ErrorMessage = "角色编码不能为空")]
    public string Code { get; set; } = string.Empty;

    /// <summary>角色描述</summary>
    public string? Description { get; set; }

    /// <summary>
    /// 目标租户ID（平台管理员可选）
    /// null = 全局角色，对所有租户可见
    /// 非null = 租户级角色，仅对该租户可见
    /// 租户管理员创建的角色自动归属当前租户
    /// </summary>
    public Guid? TenantId { get; set; }
}
