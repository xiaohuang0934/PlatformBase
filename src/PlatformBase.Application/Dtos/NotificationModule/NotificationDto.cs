namespace PlatformBase.Application.Dtos.NotificationModule;

/// <summary>
/// 通知列表 DTO
/// </summary>
public class NotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string Channel { get; set; } = "in_app";
    public DateTime Timestamp { get; set; }
}
