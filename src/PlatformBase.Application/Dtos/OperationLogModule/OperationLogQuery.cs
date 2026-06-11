using PlatformBase.Core.Models;

namespace PlatformBase.Application.Dtos.OperationLogModule;

/// <summary>
/// 操作日志分页查询条件
/// </summary>
public class OperationLogQuery : PagedRequest
{
    /// <summary>按用户 ID 筛选</summary>
    public Guid? UserId { get; set; }

    /// <summary>按操作类型筛选（login/create/update/delete）</summary>
    public string? Action { get; set; }

    /// <summary>按用户名模糊搜索</summary>
    public string? Username { get; set; }

    /// <summary>开始时间</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>结束时间</summary>
    public DateTime? EndTime { get; set; }
}
