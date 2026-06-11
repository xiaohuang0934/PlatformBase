using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Services;
using PlatformBase.Infrastructure.Data;
using StackExchange.Redis;

namespace PlatformBase.Host.Middleware;

/// <summary>
/// 安全戳验证中间件
/// 在 JwtBearer 认证通过后，校验 token 中的 security_stamp 是否与数据库一致
/// 密码变更时 security_stamp 被刷新 → 旧 token stamp 不匹配 → 返回统一 ApiResult 格式的 401
/// 
/// 缓存策略：
///   Redis GET "stamp:{userId}" → HIT → 对比 → 放行/401
///   MISS → DB SELECT SecurityStamp → 回写 Redis EX 180s → 放行/401
///   Redis 不可用 → 直接查 DB 兜底
/// 密码变更时 AuthService/UserService 主动删除 stamp 缓存，旧 token 即时失效
/// </summary>
public class StampValidationMiddleware
{
    private readonly RequestDelegate _next;

    private const string CacheKeyPrefix = "stamp:";
    private const int CacheTtlSeconds = 180;

    public StampValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context,
        ICurrentUserService currentUser,
        AppDbContext dbContext,
        IServiceProvider serviceProvider)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId == null)
        {
            await _next(context);
            return;
        }

        var tokenStamp = context.User?.FindFirst("security_stamp")?.Value;
        if (string.IsNullOrEmpty(tokenStamp))
        {
            await _next(context);
            return;
        }

        var userId = currentUser.UserId.Value.ToString();
        string? dbStamp = null;

        // ① 尝试 Redis
        var redis = serviceProvider.GetService<IConnectionMultiplexer>()?.GetDatabase();
        if (redis != null)
        {
            try
            {
                var cached = await redis.StringGetAsync($"{CacheKeyPrefix}{userId}");
                if (cached.HasValue)
                {
                    dbStamp = cached.ToString();
                }
            }
            catch { /* Redis 不可用 → 降级查 DB */ }
        }

        // ② Redis MISS 或不可用 → 查数据库
        if (dbStamp == null)
        {
            var user = await dbContext.Set<User>()
                .AsNoTracking()
                .Where(u => u.Id == currentUser.UserId.Value)
                .Select(u => new { u.SecurityStamp })
                .FirstOrDefaultAsync();

            if (user == null || user.SecurityStamp != tokenStamp)
            {
                await WriteUnauthorizedResponse(context, "Token已失效，请重新登录");
                return;
            }

            dbStamp = user.SecurityStamp;

            // ③ 回写 Redis（可选，失败不阻塞）
            if (redis != null)
            {
                try
                {
                    await redis.StringSetAsync(
                        $"{CacheKeyPrefix}{userId}", tokenStamp,
                        TimeSpan.FromSeconds(CacheTtlSeconds));
                }
                catch { /* Redis 回写失败，不影响请求继续 */ }
            }
        }
        else if (dbStamp != tokenStamp)
        {
            await WriteUnauthorizedResponse(context, "Token已失效，请重新登录");
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// 写入统一 ApiResult 格式的 401 响应，保持与项目统一响应约定一致
    /// </summary>
    private static async Task WriteUnauthorizedResponse(HttpContext context, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = 200;

        var result = ApiResult.Fail(ErrorCode.Unauthorized, message);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await context.Response.WriteAsync(json);
    }
}
