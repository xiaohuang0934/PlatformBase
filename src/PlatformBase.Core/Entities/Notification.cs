namespace PlatformBase.Core.Entities;

/// <summary>
/// 通知实体，记录发送给用户的每一条通知
/// </summary>
public class Notification : TenantAuditableEntity
{
    /// <summary>接收用户 ID</summary>
    public Guid UserId { get; set; }

    /// <summary>通知标题</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>通知内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>关联模板编码</summary>
    public string? TemplateCode { get; set; }

    /// <summary>是否已读</summary>
    public bool IsRead { get; set; }

    /// <summary>已读时间</summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>发送通道</summary>
    public string Channel { get; set; } = "in_app";

    /// <summary>通知时间</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
