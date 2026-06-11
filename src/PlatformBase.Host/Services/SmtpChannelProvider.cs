using System.Net.Mail;
using System.Text.Json;
using PlatformBase.Application.Services;
using PlatformBase.Core.Models;
using PlatformBase.Host.NotificationProviders;

namespace PlatformBase.Host.Services;

/// <summary>
/// SMTP 邮件通道实现，从 SystemParam 读取配置（支持租户覆盖 + 多配置）
/// </summary>
public class SmtpChannelProvider : IChannelProvider
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<SmtpChannelProvider> _logger;
    public string Channel => "email";

    public SmtpChannelProvider(IServiceProvider sp, ILogger<SmtpChannelProvider> logger)
    { _sp = sp; _logger = logger; }

    public async Task SendAsync(string recipient, string title, string content, CancellationToken ct = default)
    {
        var sysParam = _sp.GetRequiredService<ISystemParamService>();
        var json = await sysParam.GetValueAsync("smtp:default", ct);
        if (string.IsNullOrEmpty(json))
        {
            _logger.LogWarning("SMTP 未配置（SystemParam smtp:default 为空），跳过邮件发送");
            return;
        }

        SmtpConfig? config;
        try { config = JsonSerializer.Deserialize<SmtpConfig>(json); }
        catch { _logger.LogError("SMTP 配置 JSON 格式错误"); return; }

        if (config == null || string.IsNullOrEmpty(config.Host))
        {
            _logger.LogWarning("SMTP Host 为空，跳过邮件发送");
            return;
        }

        try
        {
            using var smtp = new SmtpClient(config.Host, config.Port);
            smtp.EnableSsl = true;
            smtp.Credentials = new System.Net.NetworkCredential(config.User, config.Password);

            var from = config.From ?? config.User;
            var mail = new MailMessage(from, recipient, title, content);
            await smtp.SendMailAsync(mail, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "邮件发送失败: {Recipient}", recipient);
            throw;
        }
    }
}
