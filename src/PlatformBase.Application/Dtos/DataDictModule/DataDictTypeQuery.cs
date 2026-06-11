using PlatformBase.Core.Models;

namespace PlatformBase.Application.Dtos.DataDictModule;

/// <summary>
/// 数据字典类型分页查询条件
/// </summary>
public class DataDictTypeQuery : PagedRequest
{
    /// <summary>是否启用筛选（null=不限）</summary>
    public bool? IsEnabled { get; set; }
}
