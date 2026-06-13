using Microsoft.AspNetCore.Mvc.Filters;           // 提供 IAsyncActionFilter / ActionExecutingContext / ActionExecutionDelegate
using PlatformBase.Core.Models;                    // 提供 OperationLogEntry / OperationLogAttribute 操作日志模型
using PlatformBase.Core.Services;                  // 提供 ICurrentUserContext 获取当前用户信息
using PlatformBase.Host.Jobs;                      // 提供 OperationLogWriterJob 后台写入任务
using PlatformBase.Infrastructure.Data;            // 提供 AppDbContext / ChangeSnapshot 变更快照

namespace PlatformBase.Host.Filters;

/// <summary>
/// 操作日志 ActionFilter，在 Controller 动作执行后异步记录操作日志
///
/// 实现逻辑：
///   1. 执行 Controller Action（await next()）并捕获执行结果
///   2. 从 Action 元数据中查找 OperationLogAttribute，若无则跳过日志记录
///   3. 确定操作资源名（优先用 Attribute.Resource，降级用 controller.action）
///   4. 若 CaptureArgs=true → 序列化请求参数为 JSON（排除 ct/CancellationToken，限制深度和长度）
///   5. 合并 AppDbContext.ChangeTracker 变更快照（EF Core 自动捕获的实体变更记录）
///   6. 构造 OperationLogEntry 对象（含用户/IP/UA/是否成功等元数据）
///   7. 通过 Hangfire BackgroundJob.Enqueue 异步入队写入任务，不阻塞 HTTP 响应
///   8. 对于 login/refresh 操作，从请求体参数的 Username 属性提取用户名（此时用户可能未认证）
/// </summary>
public class OperationLogFilter : IAsyncActionFilter
{
    /// <summary>当前用户会话上下文，用于获取 UserId / UserName</summary>
    private readonly ICurrentUserContext _currentUser;

    /// <summary>数据库上下文，用于读取 ChangeTracker 变更快照</summary>
    private readonly AppDbContext _dbContext;

    /// <summary>构造函数：通过 DI 注入 ICurrentUserContext 和 AppDbContext</summary>
    public OperationLogFilter(ICurrentUserContext currentUser, AppDbContext dbContext)
    {
        _currentUser = currentUser; // 保存用户服务引用
        _dbContext = dbContext;     // 保存数据库上下文引用
    }

    /// <summary>
    /// Action 执行后触发的日志记录逻辑
    /// 流程：执行 Action → 检查属性 → 提取参数 → 合并变更 → 构造日志 → Hangfire 入队
    /// </summary>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next(); // 执行 Controller Action 及其后续过滤器/中间件，捕获 ActionExecutedContext

        // 从 Action 的 EndpointMetadata 中查找 OperationLogAttribute 标记
        var attr = context.ActionDescriptor.EndpointMetadata
            .OfType<OperationLogAttribute>()   // 过滤出 OperationLogAttribute 类型
            .FirstOrDefault();                 // 取第一个匹配（理论上只有一个）

        if (attr == null) return; // 无 OperationLog 标记（如查询接口未标注），跳过日志记录

        // 确定操作资源名：优先取 Attribute 的 Resource 属性，否则拼接 controller.action
        var resource = attr.Resource
            ?? $"{context.ActionDescriptor.RouteValues["controller"]}.{context.ActionDescriptor.RouteValues["action"]}";

        string? detail = null; // 操作详情（请求参数 + 数据变更），初始为空
        if (attr.CaptureArgs && context.ActionArguments.Count > 0) // 仅当 Attribute 指定捕获参数且有参数时
        {
            try
            {
                // 过滤掉 CancellationToken 类参数（不可序列化也无业务意义）
                var serializableArgs = context.ActionArguments
                    .Where(kv => !kv.Key.Equals("cancellationToken", StringComparison.OrdinalIgnoreCase) // 排除标准命名
                                 && !kv.Key.Equals("ct", StringComparison.OrdinalIgnoreCase))            // 排除缩写
                    .ToDictionary(kv => kv.Key, kv => kv.Value); // 转为字典以便序列化

                if (serializableArgs.Count > 0) // 过滤后仍有可序列化的参数
                {
                    detail = System.Text.Json.JsonSerializer.Serialize(serializableArgs, // 序列化为 JSON 字符串
                        new System.Text.Json.JsonSerializerOptions { MaxDepth = 2 });     // 限制序列化深度为 2 层（防止嵌套过大）
                    if (detail.Length > 2000) detail = detail[..2000]; // 截断超过 2000 字符的详情（保护 DB 字段长度）
                }
            }
            catch { /* 序列化失败则跳过（如包含循环引用），不影响主流程 */ }
        }

        // 合并 ChangeTracker 变更快照（EF Core SaveChanges 时捕获的新增/修改/删除记录）
        var changeSnapshot = _dbContext.ChangeSnapshot; // 获取本次请求中 Entity Framework 捕获的数据变更文本
        if (!string.IsNullOrEmpty(changeSnapshot)) // 有变更时
        {
            detail = detail != null
                ? $"{detail}\nChanges:{changeSnapshot}" // 请求参数 + 变更快照（用换行分隔）
                : $"Changes:{changeSnapshot}";           // 仅有变更快照（无请求参数时）
            if (detail.Length > 4000) detail = detail[..4000]; // 合并后截断超过 4000 字符
        }

        // 构造操作日志条目
        var entry = new OperationLogEntry
        {
            UserId = _currentUser.UserId,                      // 操作用户 ID（来自 JWT）
            Username = _currentUser.UserName ?? ExtractUsernameFromArgs(context, attr), // 用户名（优先已认证用户，否则从参数提取）
            Action = attr.Action,                              // 操作类型（login/create/update/delete 等）
            Resource = resource,                               // 操作资源名
            Detail = detail,                                   // 操作详情（参数 + 数据变更）
            IpAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString(), // 客户端 IP 地址
            UserAgent = context.HttpContext.Request.Headers.UserAgent.ToString(),   // 用户代理（浏览器/客户端标识）
            IsSuccess = executed.Exception == null && context.HttpContext.Response.StatusCode < 400 // 是否成功（无异常且状态码 < 400）
        };

        // 通过 Hangfire 后台任务异步入队写入日志（不阻塞当前 HTTP 响应）
        Hangfire.BackgroundJob.Enqueue<OperationLogWriterJob>(job => job.WriteAsync(entry, CancellationToken.None));
    }

    /// <summary>
    /// 从未认证的请求体中提取用户名（用于登录/刷新等匿名操作的日志记录）
    /// 仅在 Action 为 "login" 或 "refresh" 时执行额外提取逻辑
    /// 遍历 ActionArguments，查找含有 Username 属性的参数对象
    /// </summary>
    private static string? ExtractUsernameFromArgs(ActionExecutingContext context, OperationLogAttribute attr)
    {
        if (attr.Action != "login" && attr.Action != "refresh") // 仅 login/refresh 操作需要提取用户名
            return null;

        foreach (var arg in context.ActionArguments.Values) // 遍历所有 Action 参数（如 LoginRequest、RefreshRequest）
        {
            if (arg == null) continue; // 跳过 null 参数
            var prop = arg.GetType().GetProperty("Username"); // 通过反射查找 Username 属性
            if (prop != null) // 找到 Username 属性
            {
                var val = prop.GetValue(arg) as string; // 获取属性值并转为字符串
                if (!string.IsNullOrWhiteSpace(val)) return val; // 有效用户名则直接返回
            }
        }
        return null; // 未找到用户名（通常不会发生，因为 login/refresh 必有 username 参数）
    }
}
