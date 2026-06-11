namespace PlatformBase.Host.Filters;

/// <summary>
/// API 限流标记特性，标注在需要限流的 Controller Action 上
/// 使用 Redis 滑动窗口算法，Redis 不可用时跳过限流
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class RateLimitAttribute : Attribute
{
    /// <summary>时间窗口内最大请求数</summary>
    public int Limit { get; }

    /// <summary>时间窗口秒数</summary>
    public int Seconds { get; }

    public RateLimitAttribute(int limit, int seconds)
    {
        Limit = limit;
        Seconds = seconds;
    }
}
