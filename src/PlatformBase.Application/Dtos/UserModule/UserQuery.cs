using PlatformBase.Core.Models;

namespace PlatformBase.Application.Dtos.UserModule;

/// <summary>
/// 用户分页查询条件
/// </summary>
public class UserQuery : PagedRequest
{
    /// <summary>是否启用筛选（null=不限）</summary>
    public bool? IsActive { get; set; }
}
