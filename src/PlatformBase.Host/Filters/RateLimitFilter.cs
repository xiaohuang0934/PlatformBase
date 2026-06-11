using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using StackExchange.Redis;

namespace PlatformBase.Host.Filters;

/// <summary>
/// API 限流 ActionFilter，基于 Redis 滑动窗口算法
/// Key: "ratelimit:{ip}:{endpoint}"，使用 ZSet 时间戳排序
/// </summary>
public class RateLimitFilter : IAsyncActionFilter
{
    private readonly IDatabase? _redis;

    public RateLimitFilter(IServiceProvider serviceProvider)
    {
        _redis = serviceProvider.GetService<IConnectionMultiplexer>()?.GetDatabase();
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var attr = context.ActionDescriptor.EndpointMetadata
            .OfType<RateLimitAttribute>()
            .FirstOrDefault();

        if (attr == null || _redis == null)
        {
            await next();
            return;
        }

        var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var endpoint = context.HttpContext.Request.Path.ToString();
        var key = $"ratelimit:{ip}:{endpoint}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowStart = now - (long)attr.Seconds * 1000;

        try
        {
            // 清理过期窗口 + 添加当前时间戳 + 统计
            await _redis.SortedSetRemoveRangeByScoreAsync(key, 0, windowStart);
            await _redis.SortedSetAddAsync(key, now, now); // score = member = timestamp
            await _redis.KeyExpireAsync(key, TimeSpan.FromSeconds(attr.Seconds + 1));

            var count = await _redis.SortedSetLengthAsync(key);
            if (count > attr.Limit)
            {
                context.Result = new ContentResult
                {
                    Content = JsonSerializer.Serialize(
                        ApiResult.Fail(ErrorCode.TooManyRequests, "请求过于频繁，请稍后重试"),
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    ContentType = "application/json",
                    StatusCode = 200
                };
                return;
            }
        }
        catch
        {
            // Redis 不可用 → 跳过限流
        }

        await next();
    }
}
