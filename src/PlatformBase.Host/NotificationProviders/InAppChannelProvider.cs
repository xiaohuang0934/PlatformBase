namespace PlatformBase.Host.NotificationProviders;

/// <summary>
/// 站内信通道实现（默认），通知直接存入 Notifications 表
/// </summary>
public class InAppChannelProvider : IChannelProvider
{
    public string Channel => "in_app";

    public Task SendAsync(string recipient, string title, string content, CancellationToken ct = default)
    {
        // 站内信由 NotificationService 直接入库，此处不重复处理
        return Task.CompletedTask;
    }
}
