using PlatformBase.Core.Models;

namespace PlatformBase.Application.Dtos;

/// <summary>
/// 系统参数分页查询条件
/// </summary>
public class SystemParamQuery : PagedRequest
{
    /// <summary>按分类筛选（可选）</summary>
    public string? Category { get; set; }

    /// <summary>是否启用筛选（null=不限）</summary>
    public bool? IsEnabled { get; set; }
}
