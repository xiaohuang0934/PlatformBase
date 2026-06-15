using System.Text.RegularExpressions;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Repositories;
using PlatformBase.Host.NotificationProviders;

namespace PlatformBase.Host.Services.NotificationModule;

/// <summary>
/// 消息通知服务实现
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _uow;
    private readonly IEnumerable<IChannelProvider> _providers;

    public NotificationService(IUnitOfWork uow, IEnumerable<IChannelProvider> providers)
    {
        _uow = uow;
        _providers = providers;
    }

    public async Task SendByTemplateAsync(Guid userId, string templateCode,
        Dictionary<string, string>? variables = null, CancellationToken ct = default)
    {
        var template = await _uow.Repository<NotificationTemplate>()
            .FirstOrDefaultAsync(t => t.Code == templateCode && t.IsEnabled, ct);
        if (template == null)
            throw new BusinessException($"通知模板 '{templateCode}' 不存在", ErrorCode.DataNotFound);

        var title = ReplaceVariables(template.TitleTemplate, variables);
        var content = ReplaceVariables(template.BodyTemplate, variables);

        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Content = content,
            TemplateCode = templateCode,
            Channel = template.Channel,
            Timestamp = DateTime.UtcNow
        };

        await _uow.Repository<Notification>().AddAsync(notification, ct);
        await _uow.SaveChangesAsync(ct);

        // 通过通道提供商发送（邮件/短信等扩展）
        var provider = _providers.FirstOrDefault(p => p.Channel == template.Channel);
        if (provider != null && template.Channel != "in_app")
        {
            try { await provider.SendAsync(userId.ToString(), title, content, ct); }
            catch { /* 通道失败不阻塞通知入库 */ }
        }
    }

    public async Task SendAsync(Guid userId, string title, string content, string channel = "in_app",
        CancellationToken ct = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Content = content,
            Channel = channel,
            Timestamp = DateTime.UtcNow
        };

        await _uow.Repository<Notification>().AddAsync(notification, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationDto>> GetPagedAsync(Guid userId, bool? unreadOnly = null,
        int pageIndex = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var items = await _uow.Repository<Notification>()
            .GetPagedAsync(new PagedRequest
            {
                PageIndex = pageIndex,
                PageSize = pageSize,
                SortField = nameof(Notification.Timestamp),
                IsAscending = false
            }, filter: n => n.UserId == userId && (!unreadOnly.HasValue || !unreadOnly.Value || !n.IsRead), ct);

        return items.Items.Select(ToDto).ToList();
    }

    public async Task MarkAsReadAsync(Guid notificationId, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Notification>().GetByIdAsync(notificationId, ct);
        if (entity == null) return;

        entity.IsRead = true;
        entity.ReadAt = DateTime.UtcNow;
        _uow.Repository<Notification>().Update(entity);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        var unreadItems = await _uow.Repository<Notification>()
            .FindAsync(n => n.UserId == userId && !n.IsRead, ct);

        foreach (var item in unreadItems)
        {
            item.IsRead = true;
            item.ReadAt = DateTime.UtcNow;
        }

        if (unreadItems.Count > 0)
        {
            _uow.Repository<Notification>().UpdateRange(unreadItems);
            await _uow.SaveChangesAsync(ct);
        }
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        return await _uow.Repository<Notification>()
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);
    }

    private static string ReplaceVariables(string template, Dictionary<string, string>? variables)
    {
        if (variables == null) return template;
        return Regex.Replace(template, @"\{(\w+)\}", match =>
            variables.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
    }

    private static NotificationDto ToDto(Notification entity) => new()
    {
        Id = entity.Id,
        Title = entity.Title,
        Content = entity.Content,
        IsRead = entity.IsRead,
        ReadAt = entity.ReadAt,
        Channel = entity.Channel,
        Timestamp = entity.Timestamp
    };
}
