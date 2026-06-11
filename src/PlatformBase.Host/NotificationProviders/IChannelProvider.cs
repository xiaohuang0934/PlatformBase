namespace PlatformBase.Host.NotificationProviders;

/// <summary>
/// 通知通道抽象，支持站内信/邮件/短信可插拔扩展
/// </summary>
public interface IChannelProvider
{
    /// <summary>通道名称（in_app / email / sms）</summary>
    string Channel { get; }

    /// <summary>发送通知</summary>
    Task SendAsync(string recipient, string title, string content, CancellationToken ct = default);
}
