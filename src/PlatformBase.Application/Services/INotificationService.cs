using PlatformBase.Application.Dtos;

namespace PlatformBase.Application.Services;

/// <summary>
/// 消息通知服务接口
/// </summary>
public interface INotificationService
{
    /// <summary>通过模板发送通知</summary>
    Task SendByTemplateAsync(Guid userId, string templateCode, Dictionary<string, string>? variables = null,
        CancellationToken ct = default);

    /// <summary>直接发送通知</summary>
    Task SendAsync(Guid userId, string title, string content, string channel = "in_app",
        CancellationToken ct = default);

    /// <summary>分页查询用户通知</summary>
    Task<IReadOnlyList<NotificationDto>> GetPagedAsync(Guid userId, bool? unreadOnly = null,
        int pageIndex = 1, int pageSize = 20, CancellationToken ct = default);

    /// <summary>标记单条已读</summary>
    Task MarkAsReadAsync(Guid notificationId, CancellationToken ct = default);

    /// <summary>全部标记已读</summary>
    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);

    /// <summary>获取未读数</summary>
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
}
