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
///
/// 实现逻辑：
///   1. 从 Action 元数据中查找 RateLimitAttribute，获取 limit（上限次数）和 seconds（窗口长度）
///   2. 若无标记或 Redis 不可用 → 直接放行，不阻塞请求
///   3. 构造 Redis Key = "ratelimit:{客户端IP}:{请求路径}"
///   4. 计算当前窗口起始时间 = now - seconds*1000（毫秒时间戳）
///   5. 原子操作（3 步顺序执行）：
///      a. 清理窗口外的过期记录（ZREMRANGEBYSCORE 0 → windowStart）
///      b. 添加当前请求时间戳（ZADD key now now）—— score 和 member 均用时间戳
///      c. 设置 Key 过期时间（EXPIRE key seconds+1）—— 窗口结束自动清理，防止 Key 堆积
///   6. 统计窗口内记录数（ZCARD），若 > limit → 返回 200 + ApiResult.Fail(TooManyRequests)
///   7. 若 Redis 命令抛出异常 → 跳过限流，保证核心业务可用
/// </summary>
public class RateLimitFilter : IAsyncActionFilter
{
    /// <summary>Redis 数据库实例（可能为 null，当 Redis 服务不可用时）</summary>
    private readonly IDatabase? _redis;

    /// <summary>
    /// 构造函数：通过 IServiceProvider 延迟获取 Redis 连接
    /// 不使用构造函数注入 IDatabase 是为了防止 Redis 不可用时整个 DI 链崩溃
    /// </summary>
    public RateLimitFilter(IServiceProvider serviceProvider)
    {
        _redis = serviceProvider.GetService<IConnectionMultiplexer>()?.GetDatabase(); // 从容器取 IConnectionMultiplexer，再获取 DB（null 安全）
    }

    /// <summary>
    /// Action 执行前的限流检查
    /// 无 RateLimit 标记或 Redis 不可用 → 直接放行
    /// 超限 → 返回 200 错误响应并拦截后续执行
    /// </summary>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 从 Action 元数据中查找 RateLimitAttribute 标记
        var attr = context.ActionDescriptor.EndpointMetadata
            .OfType<RateLimitAttribute>()   // 过滤出 RateLimitAttribute 类型
            .FirstOrDefault();              // 取第一个匹配

        if (attr == null || _redis == null) // 无标记（不需要限流）或 Redis 不可用（降级放行）
        {
            await next(); // 继续执行 Action
            return;
        }

        var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"; // 获取客户端 IP
        var endpoint = context.HttpContext.Request.Path.ToString(); // 获取请求路径（如 /api/v1/users）
        var key = $"ratelimit:{ip}:{endpoint}"; // 构造 Redis Key：ratelimit:192.168.1.1:/api/v1/users
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); // 当前 UTC 时间毫秒时间戳
        var windowStart = now - (long)attr.Seconds * 1000; // 滑动窗口起始时间（当前时间 - 窗口秒数*1000 毫秒）

        try
        {
            // 步骤 1：清理窗口外的过期记录（ZREMRANGEBYSCORE key 0 windowStart）
            await _redis.SortedSetRemoveRangeByScoreAsync(key, 0, windowStart);

            // 步骤 2：添加当前请求时间戳（ZADD key now now），score=member=时间戳（member 唯一性由时间戳保证）
            await _redis.SortedSetAddAsync(key, now, now);

            // 步骤 3：设置 Key 过期时间（EXPIRE key seconds+1），+1 秒防止边界问题
            await _redis.KeyExpireAsync(key, TimeSpan.FromSeconds(attr.Seconds + 1));

            // 步骤 4：统计窗口内请求次数（ZCARD key）
            var count = await _redis.SortedSetLengthAsync(key);
            if (count > attr.Limit) // 超过上限次数
            {
                // 构造限流拒绝响应（HTTP 200 + ApiResult.Fail）
                context.Result = new ContentResult
                {
                    Content = JsonSerializer.Serialize(
                        ApiResult.Fail(ErrorCode.TooManyRequests, "请求过于频繁，请稍后重试"), // 错误码 + 提示信息
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), // 驼峰命名
                    ContentType = "application/json", // 响应类型：JSON
                    StatusCode = 200                   // HTTP 状态码：200（前端统一按业务码判断）
                };
                return; // 拦截请求，不执行后续 Action
            }
        }
        catch
        {
            // Redis 命令执行异常（如网络超时/Redis 宕机）→ 跳过限流，保证业务可用性（降级策略）
        }

        await next(); // 未超限 → 继续执行后续 Action
    }
}
