namespace PlatformBase.Core.Models;

/// <summary>
/// 操作日志条目（POCO），由 ActionFilter 构造后通过 Hangfire 异步入队
/// </summary>
public class OperationLogEntry
{
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsSuccess { get; set; } = true;
}
