using PlatformBase.Core.Models;

namespace PlatformBase.Application.Dtos.PermissionModule;

/// <summary>
/// 权限分页查询条件
/// </summary>
public class PermissionQuery : PagedRequest
{
    /// <summary>按接口路径筛选</summary>
    public string? ResourcePath { get; set; }

    /// <summary>按分组筛选</summary>
    public string? GroupName { get; set; }

    /// <summary>是否启用筛选（null=不限）</summary>
    public bool? IsEnabled { get; set; }
}
