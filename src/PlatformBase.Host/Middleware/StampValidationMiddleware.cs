using System.Text.Json;                    // 提供 JsonSerializer / JsonSerializerOptions 序列化 401 响应
using Microsoft.EntityFrameworkCore;           // 提供 AsNoTracking / FirstOrDefaultAsync 等查询扩展
using PlatformBase.Core.Entities;              // 提供 User 实体（含 SecurityStamp 字段）
using PlatformBase.Core.Exceptions;           // 提供 ErrorCode 枚举（Unauthorized 等）
using PlatformBase.Core.Models;               // 提供 ApiResult 统一响应模型
using PlatformBase.Core.Services;             // 提供 ICurrentUserContext 获取当前用户信息
using PlatformBase.Infrastructure.Data;       // 提供 AppDbContext 数据库上下文
using StackExchange.Redis;                    // 提供 IDatabase / StringGetAsync / StringSetAsync

namespace PlatformBase.Host.Middleware;

/// <summary>
/// 安全戳验证中间件 — 密码变更后旧 Token 即时失效（方案 A：Redis + DB 双校验）
/// 在 JwtBearer 认证通过后执行，校验 token 中的 security_stamp 是否与数据库一致
///
/// 实现逻辑：
///   1. 从 JWT Token 的 security_stamp Claim 中提取当前令牌的安全戳
///   2. 先查 Redis 缓存（KEY: "stamp:{userId}"）：
///      a. HIT → 直接对比 Token stamp vs Redis stamp → 匹配则放行，不匹配返回 401
///      b. MISS → 查 DB（User 表的 SecurityStamp 字段）
///         - 匹配失败 → 立即返回 401（密码已被他人修改）
///         - 匹配成功 → 回写 Redis 缓存（TTL=180s），继续放行
///   3. Redis 不可用时降级为直接查 DB（保证核心功能可用）
///   4. 密码变更时 AuthService/UserService 主动删除 stamp 缓存 → 旧 Token 下次请求必然 MISS → 查 DB → 不匹配 → 401
///   5. 未认证用户或无 security_stamp Claim → 直接放行（不适用此校验）
/// </summary>
public class StampValidationMiddleware
{
    /// <summary>下一个中间件委托（管道中的后续处理程序）</summary>
    private readonly RequestDelegate _next;

    /// <summary>Redis 缓存 Key 前缀：stamp:</summary>
    private const string CacheKeyPrefix = "stamp:";

    /// <summary>缓存有效期：180 秒（密码变更后 3 分钟内旧 Token 自动失效）</summary>
    private const int CacheTtlSeconds = 180;

    /// <summary>构造函数：注入 RequestDelegate（管道中的下一个中间件）</summary>
    public StampValidationMiddleware(RequestDelegate next)
    {
        _next = next; // 保存下一个中间件引用
    }

    /// <summary>
    /// 中间件入口：从 DI 容器解析所需服务（middleware 注入支持 scoped/transient 服务）
    /// 流程：检查认证状态 → 提取 Token stamp → Redis 缓存查询 → DB 兜底 → 对比 → 放行/拒绝
    /// </summary>
    public async Task InvokeAsync(HttpContext context,
        ICurrentUserContext currentUser,   // 当前用户服务（判断是否已认证）
        AppDbContext dbContext,            // 数据库上下文（查询 User.SecurityStamp）
        IServiceProvider serviceProvider)  // 服务提供者（延迟获取 Redis 连接，避免不可用时崩溃）
    {
        // 未认证用户 → 跳过安全戳校验（无 Token 自然无需验证 stamp）
        if (!currentUser.IsAuthenticated || currentUser.UserId == null)
        {
            await _next(context); // 继续管道
            return;
        }

        // 从 JWT Token 中提取 security_stamp Claim（签发时写入的当时密码安全戳）
        var tokenStamp = context.User?.FindFirst("security_stamp")?.Value;
        if (string.IsNullOrEmpty(tokenStamp)) // Token 中无 stamp（可能是旧 Token 或未配置）
        {
            await _next(context); // 放行（向后兼容：无 stamp 的旧 Token 仍可用）
            return;
        }

        var userId = currentUser.UserId.Value.ToString(); // 用户 ID 转字符串（用于构造缓存 Key）
        string? dbStamp = null;                          // 数据库中/缓存中的安全戳

        // ① 尝试从 Redis 缓存获取安全戳（快速路径）
        var redis = serviceProvider.GetService<IConnectionMultiplexer>()?.GetDatabase(); // 延迟获取 Redis（容错：null 时走 DB）
        if (redis != null) // Redis 服务可用
        {
            try
            {
                var cached = await redis.StringGetAsync($"{CacheKeyPrefix}{userId}"); // GET stamp:{userId}
                if (cached.HasValue) // 缓存命中
                {
                    dbStamp = cached.ToString(); // 取缓存中的 stamp 值
                }
            }
            catch { /* Redis 不可用（网络超时/宕机）→ 降级查 DB，不中断请求 */ }
        }

        // ② Redis MISS 或不可用 → 查数据库获取当前安全戳（兜底路径）
        if (dbStamp == null)
        {
            // 查询 User 表：只取 SecurityStamp 字段（最小化数据传输）
            var user = await dbContext.Set<User>()               // 获取 User 表 DbSet（避免硬依赖 DbContext 属性）
                .AsNoTracking()                                   // 只读查询，无需变更追踪
                .Where(u => u.Id == currentUser.UserId.Value)    // 按用户 ID 筛选
                .Select(u => new { u.SecurityStamp })             // 仅投影 SecurityStamp 字段
                .FirstOrDefaultAsync();                           // 取第一条（用户 ID 唯一）

            // 用户不存在 或 stamp 不匹配 → Token 已失效
            if (user == null || user.SecurityStamp != tokenStamp)
            {
                await WriteUnauthorizedResponse(context, "Token已失效，请重新登录"); // 返回 401 统一格式
                return; // 中断管道，不执行后续中间件/Controller
            }

            dbStamp = user.SecurityStamp; // 数据库中的有效 stamp（用于后续回写缓存）

            // ③ 缓存命中后回写 Redis（可选操作，失败不阻塞请求）
            if (redis != null) // Redis 可用时才回写
            {
                try
                {
                    await redis.StringSetAsync(
                        $"{CacheKeyPrefix}{userId}", tokenStamp,  // SET stamp:{userId} {tokenStamp}
                        TimeSpan.FromSeconds(CacheTtlSeconds));    // EX 180 秒后自动过期
                }
                catch { /* Redis 回写失败（网络波动等），不影响请求继续放行 */ }
            }
        }
        // ④ Redis 缓存命中 → 对比 Token stamp 与缓存中的 stamp
        else if (dbStamp != tokenStamp) // 缓存中的 stamp 与 Token 中的 stamp 不一致
        {
            await WriteUnauthorizedResponse(context, "Token已失效，请重新登录"); // 返回 401，密码已被修改
            return;
        }

        await _next(context); // stamp 一致 → 放行，继续执行后续中间件和 Controller
    }

    /// <summary>
    /// 写入统一 ApiResult 格式的 401 响应，保持与项目统一响应约定一致
    /// HTTP 200 + body: { "code": Unauthorized, "message": "..." }
    /// </summary>
    /// <param name="context">当前 HTTP 上下文</param>
    /// <param name="message">前端提示信息</param>
    private static async Task WriteUnauthorizedResponse(HttpContext context, string message)
    {
        context.Response.ContentType = "application/json"; // 设置响应类型为 JSON
        context.Response.StatusCode = 200;                 // HTTP 状态码：200（前端统一按 body 中 code 判断）

        var result = ApiResult.Fail(ErrorCode.Unauthorized, message); // 构造统一错误响应
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase // 驼峰命名
        });
        await context.Response.WriteAsync(json); // 写入响应体
    }
}
