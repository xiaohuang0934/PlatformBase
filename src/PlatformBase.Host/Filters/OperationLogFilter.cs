using Microsoft.AspNetCore.Mvc.Filters;
using PlatformBase.Core.Models;
using PlatformBase.Core.Services;
using PlatformBase.Host.Jobs;

namespace PlatformBase.Host.Filters;

/// <summary>
/// 操作日志 ActionFilter，在 Controller 动作执行后异步记录操作日志
/// 通过 Hangfire Enqueue 异步入队，不阻塞 HTTP 响应
/// </summary>
public class OperationLogFilter : IAsyncActionFilter
{
    private readonly ICurrentUserService _currentUser;

    public OperationLogFilter(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
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
                // 仅序列化非框架参数（过滤 CancellationToken 等不可序列化类型）
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
            catch { /* 序列化失败则跳过 detail 捕获 */ }
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

        // 异步入队不阻塞 HTTP 响应
        Hangfire.BackgroundJob.Enqueue<OperationLogWriterJob>(job => job.WriteAsync(entry, CancellationToken.None));
    }
}
