using PlatformBase.Core.Models;

namespace PlatformBase.Application.Dtos.RoleModule;

/// <summary>
/// 角色分页查询条件
/// </summary>
public class RoleQuery : PagedRequest
{
    /// <summary>是否系统角色</summary>
    public bool? IsSystem { get; set; }

    /// <summary>
    /// 指定查询的租户ID列表（平台用户专用）。
    /// null = 使用 ICurrentUserContext.CurrentTenantIds；
    /// 非 null = 校验是否在 TenantIds 范围内后使用此列表。
    /// </summary>
    public List<Guid>? TenantIds { get; set; }
}
