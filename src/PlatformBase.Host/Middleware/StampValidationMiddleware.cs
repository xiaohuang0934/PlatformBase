using Microsoft.EntityFrameworkCore;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Services;
using PlatformBase.Infrastructure.Data;
using StackExchange.Redis;

namespace PlatformBase.Host.Middleware;

/// <summary>
/// 安全戳验证中间件（方案 A）
/// 在 JwtBearer 认证通过后，校验 token 中的 security_stamp 是否与数据库一致
/// 密码变更时 security_stamp 被刷新 → 旧 token 中的 stamp 不匹配 → 返回 401
/// 
/// 缓存策略：
///   Redis GET "stamp:{userId}" → HIT → 对比 → 放行/401
///   MISS → DB SELECT SecurityStamp → 回写 Redis EX 60s → 放行/401
///   Redis 不可用 → 直接查 DB 兜底
/// </summary>
public class StampValidationMiddleware
{
    private readonly RequestDelegate _next;

    private const string CacheKeyPrefix = "stamp:";
    private const int CacheTtlSeconds = 60;

    public StampValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context,
        ICurrentUserService currentUser,
        AppDbContext dbContext,
        IServiceProvider serviceProvider)
    {
        // 仅校验已认证的请求
        if (!currentUser.IsAuthenticated || currentUser.UserId == null)
        {
            await _next(context);
            return;
        }

        // 提取 token 中的 security_stamp
        var tokenStamp = context.User?.FindFirst("security_stamp")?.Value;
        if (string.IsNullOrEmpty(tokenStamp))
        {
            await _next(context);
            return;
        }

        var userId = currentUser.UserId.Value.ToString();

        // ① 尝试 Redis
        var redis = serviceProvider.GetService<IConnectionMultiplexer>()?.GetDatabase();
        if (redis != null)
        {
            try
            {
                var cached = await redis.StringGetAsync($"{CacheKeyPrefix}{userId}");
                if (cached.HasValue)
                {
                    if (cached.ToString() != tokenStamp)
                    {
                        context.Response.StatusCode = 401;
                        return;
                    }
                    await _next(context);
                    return;
                }
            }
            catch { /* Redis 不可用 → 降级查 DB */ }
        }

        // ② Redis MISS 或不可用 → 查数据库
        var user = await dbContext.Set<User>()
            .AsNoTracking()
            .Where(u => u.Id == currentUser.UserId.Value)
            .Select(u => new { u.SecurityStamp })
            .FirstOrDefaultAsync();

        if (user == null || user.SecurityStamp != tokenStamp)
        {
            context.Response.StatusCode = 401;
            return;
        }

        // ③ 回写 Redis（可选，失败不阻塞）
        if (redis != null)
        {
            try
            {
                await redis.StringSetAsync(
                    $"{CacheKeyPrefix}{userId}", tokenStamp,
                    TimeSpan.FromSeconds(CacheTtlSeconds));
            }
            catch { }
        }

        await _next(context);
    }
}
