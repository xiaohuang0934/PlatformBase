namespace PlatformBase.Application.Dtos.OperationLogModule;

/// <summary>
/// 操作日志列表 DTO
/// </summary>
public class OperationLogDto
{
    public Guid Id { get; set; }
    public string? Username { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? IpAddress { get; set; }
    public bool IsSuccess { get; set; }
    public DateTime Timestamp { get; set; }
}
