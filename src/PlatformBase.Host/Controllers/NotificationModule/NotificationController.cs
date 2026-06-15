using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Repositories;
using PlatformBase.Core.Services;
using PlatformBase.Host.Authorization;

namespace PlatformBase.Host.Controllers.NotificationModule;

/// <summary>
/// 消息通知 API 控制器（含用户通知 + 模板管理）
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/notifications")]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _service;
    private readonly ICurrentUserContext _currentUser;
    private readonly IUnitOfWork _uow;

    public NotificationController(INotificationService service, ICurrentUserContext currentUser, IUnitOfWork uow)
    {
        _service = service;
        _currentUser = currentUser;
        _uow = uow;
    }

    /// <summary>获取当前用户的通知列表</summary>
    [HttpGet]
    [Authorize]
    public async Task<ApiResult<object>> GetPaged(
        [FromQuery] bool? unreadOnly = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (_currentUser.UserId == null)
            return ApiResult<object>.Fail(ErrorCode.Unauthorized, "未登录");
        var userId = _currentUser.UserId.Value;
        var items = await _service.GetPagedAsync(userId, unreadOnly, pageIndex, pageSize, ct);
        var unreadCount = await _service.GetUnreadCountAsync(userId, ct);

        return ApiResult<object>.Ok(new { items, unreadCount });
    }

    /// <summary>标记单条已读</summary>
    [HttpPatch("{id:guid}/read")]
    [Authorize]
    public async Task<ApiResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        await _service.MarkAsReadAsync(id, ct);
        return ApiResult.Ok("已标记已读");
    }

    /// <summary>全部标记已读</summary>
    [HttpPatch("read-all")]
    [Authorize]
    public async Task<ApiResult> MarkAllAsRead(CancellationToken ct)
    {
        if (_currentUser.UserId == null)
            return ApiResult.Fail(ErrorCode.Unauthorized, "未登录");
        await _service.MarkAllAsReadAsync(_currentUser.UserId.Value, ct);
        return ApiResult.Ok("已全部标记已读");
    }

    // ═══════════════════ 通知模板管理 ═══════════════════

    /// <summary>获取所有通知模板</summary>
    [HttpGet("templates")]
    [Permission("notifications.manage")]
    public async Task<ApiResult<IReadOnlyList<object>>> GetTemplates(CancellationToken ct)
    {
        var items = await _uow.Repository<NotificationTemplate>().FindAsync(t => t.IsEnabled, ct);
        var dtos = items.Select(t => (object)new { t.Id, t.Code, t.Name, t.TitleTemplate, t.BodyTemplate, t.Channel, t.Variables });
        return ApiResult<IReadOnlyList<object>>.Ok(dtos.ToList());
    }

    /// <summary>创建通知模板</summary>
    [HttpPost("templates")]
    [Permission("notifications.manage")]
    public async Task<ApiResult<object>> CreateTemplate([FromBody] object body, CancellationToken ct)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(
            JsonSerializer.Serialize(body));
        var entity = new NotificationTemplate
        {
            Code = json.GetProperty("code").GetString()!,
            Name = json.GetProperty("name").GetString()!,
            TitleTemplate = json.GetProperty("titleTemplate").GetString()!,
            BodyTemplate = json.GetProperty("bodyTemplate").GetString()!,
            Channel = json.TryGetProperty("channel", out var ch) ? ch.GetString()! : "in_app",
            Variables = json.TryGetProperty("variables", out var v) ? v.GetString() : null
        };
        var created = await _uow.Repository<NotificationTemplate>().AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return ApiResult<object>.Ok(new { created.Id, created.Code, created.Name });
    }

    /// <summary>更新通知模板</summary>
    [HttpPut("templates/{id:guid}")]
    [Permission("notifications.manage")]
    public async Task<ApiResult> UpdateTemplate(Guid id, [FromBody] object body, CancellationToken ct)
    {
        var t = await _uow.Repository<NotificationTemplate>().GetByIdAsync(id, ct);
        if (t == null) return ApiResult.Fail(ErrorCode.DataNotFound, "模板不存在");
        var json = JsonSerializer.Deserialize<JsonElement>(
            JsonSerializer.Serialize(body));
        if (json.TryGetProperty("titleTemplate", out var tt)) t.TitleTemplate = tt.GetString()!;
        if (json.TryGetProperty("bodyTemplate", out var bt)) t.BodyTemplate = bt.GetString()!;
        _uow.Repository<NotificationTemplate>().Update(t);
        await _uow.SaveChangesAsync(ct);
        return ApiResult.Ok("更新成功");
    }

    /// <summary>停用通知模板</summary>
    [HttpDelete("templates/{id:guid}")]
    [Permission("notifications.manage")]
    public async Task<ApiResult> DeleteTemplate(Guid id, CancellationToken ct)
    {
        var t = await _uow.Repository<NotificationTemplate>().GetByIdAsync(id, ct);
        if (t == null) return ApiResult.Fail(ErrorCode.DataNotFound, "模板不存在");
        t.IsEnabled = false;
        _uow.Repository<NotificationTemplate>().Update(t);
        await _uow.SaveChangesAsync(ct);
        return ApiResult.Ok("已停用");
    }
}
