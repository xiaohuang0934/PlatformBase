using Microsoft.AspNetCore.Mvc.Filters;
using PlatformBase.Core.Models;
using PlatformBase.Core.Services;
using PlatformBase.Host.Jobs;
using PlatformBase.Infrastructure.Data;

namespace PlatformBase.Host.Filters;

/// <summary>
/// 操作日志 ActionFilter，在 Controller 动作执行后异步记录操作日志
/// 通过 Hangfire Enqueue 异步入队，不阻塞 HTTP 响应
/// 自动捕获 AppDbContext.ChangeTracker 的数据变更快照
/// </summary>
public class OperationLogFilter : IAsyncActionFilter
{
    private readonly ICurrentUserService _currentUser;
    private readonly AppDbContext _dbContext;

    public OperationLogFilter(ICurrentUserService currentUser, AppDbContext dbContext)
    {
        _currentUser = currentUser;
        _dbContext = dbContext;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        var attr = context.ActionDescriptor.EndpointMetadata
            .OfType<OperationLogAttribute>()
            .FirstOrDefault();

        if (attr == null) return;

        var resource = attr.Resource
            ?? $"{context.ActionDescriptor.RouteValues["controller"]}.{context.ActionDescriptor.RouteValues["action"]}";

        string? detail = null;
        if (attr.CaptureArgs && context.ActionArguments.Count > 0)
        {
            try
            {
                var serializableArgs = context.ActionArguments
                    .Where(kv => !kv.Key.Equals("cancellationToken", StringComparison.OrdinalIgnoreCase)
                                 && !kv.Key.Equals("ct", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(kv => kv.Key, kv => kv.Value);

                if (serializableArgs.Count > 0)
                {
                    detail = System.Text.Json.JsonSerializer.Serialize(serializableArgs,
                        new System.Text.Json.JsonSerializerOptions { MaxDepth = 2 });
                    if (detail.Length > 2000) detail = detail[..2000];
                }
            }
            catch { /* 序列化失败则跳过 */ }
        }

        // 合并 ChangeTracker 变更快照
        var changeSnapshot = _dbContext.ChangeSnapshot;
        if (!string.IsNullOrEmpty(changeSnapshot))
        {
            detail = detail != null
                ? $"{detail}\nChanges:{changeSnapshot}"
                : $"Changes:{changeSnapshot}";
            if (detail.Length > 4000) detail = detail[..4000];
        }

        var entry = new OperationLogEntry
        {
            UserId = _currentUser.UserId,
            Username = _currentUser.UserName,
            Action = attr.Action,
            Resource = resource,
            Detail = detail,
            IpAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.HttpContext.Request.Headers.UserAgent.ToString(),
            IsSuccess = executed.Exception == null && context.HttpContext.Response.StatusCode < 400
        };

        Hangfire.BackgroundJob.Enqueue<OperationLogWriterJob>(job => job.WriteAsync(entry, CancellationToken.None));
    }
}
