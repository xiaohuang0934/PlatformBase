using System.Net;                             // 提供 HttpStatusCode 枚举（OK=200）
using System.Text.Json;                        // 提供 JsonSerializer / JsonSerializerOptions 序列化响应
using Microsoft.EntityFrameworkCore;           // 提供 DbUpdateConcurrencyException 并发冲突异常
using PlatformBase.Core.Exceptions;           // 提供 BusinessException 自定义业务异常
using PlatformBase.Core.Models;               // 提供 ApiResult 统一响应模型

namespace PlatformBase.Host.Middleware;

/// <summary>
/// 全局异常处理中间件 — 管道最先注册，捕获所有后续中间件和 Controller 抛出的未处理异常
/// 将业务异常和未处理异常统一转换为 ApiResult 格式，保持 HTTP 200 + 统一响应体
///
/// 实现逻辑：
///   1. 注册在中间件管道最前端（在 UseSerilogRequestLogging 之前）
///   2. await _next(context) 调用后续管道，用 try/catch 包裹整个请求生命周期
///   3. 异常分三类处理：
///      a. DbUpdateConcurrencyException（EF Core 并发冲突）→ LogWarning + ErrorCode.Conflict
///      b. BusinessException（业务异常，带自定义 Code）→ LogWarning + ex.Code
///      c. Exception（所有未预期的异常）→ LogError + ErrorCode.InternalError
///   4. WriteErrorResponse 将异常信息序列化为 {"code":xxx,"message":"xxx","data":null} JSON 格式
///   5. HTTP 状态码恒为 200（项目约定：前端统一按 body 中的 code 判断成功/失败）
///   6. 开发环境下 InternalError 返回完整堆栈（ex.ToString()），生产环境仅返回 "Internal server error"
/// </summary>
public class GlobalExceptionMiddleware
{
    /// <summary>下一个中间件委托（管道中的后续处理程序）</summary>
    private readonly RequestDelegate _next;

    /// <summary>结构化日志记录器，用于记录异常详情</summary>
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    /// <summary>宿主环境信息，用于判断是否为开发环境（开发环境返回详细异常信息）</summary>
    private readonly IWebHostEnvironment _env;

    /// <summary>构造函数：注入 RequestDelegate / ILogger / IWebHostEnvironment</summary>
    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IWebHostEnvironment env)
    {
        _next = next;     // 保存管道中的下一个中间件
        _logger = logger; // 保存日志记录器
        _env = env;       // 保存环境信息（判断 Development/Production）
    }

    /// <summary>
    /// 中间件入口
    /// 包裹后续管道调用在 try/catch 中，按异常类型分发处理
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context); // 调用后续中间件管道（包括 Controller 的 Action 执行）
        }
        catch (DbUpdateConcurrencyException ex) // 捕获 EF Core 并发冲突异常（乐观锁冲突）
        {
            _logger.LogWarning(ex, "并发冲突：数据已被他人修改"); // 记录警告日志（并发冲突属于预期内异常）
            await WriteErrorResponse(context, ErrorCode.Conflict, "数据已被他人修改，请刷新后重试"); // 返回 200 + Conflict 错误码
        }
        catch (BusinessException ex) // 捕获自定义业务异常（如"用户名已存在"/"余额不足"）
        {
            _logger.LogWarning(ex, "Business exception: {Message}", ex.Message); // 记录警告日志（业务异常是正常业务流程的一部分）
            await WriteErrorResponse(context, ex.Code, ex.Message); // 返回 200 + 业务自定义错误码和提示信息
        }
        catch (Exception ex) // 捕获所有其他未预期的异常（NullReference / SqlException / HttpRequestException 等）
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message); // 记录错误日志（需人工关注排查）
            var message = _env.IsDevelopment() ? ex.ToString() : "Internal server error"; // 开发环境返回完整堆栈，生产环境隐藏详情
            await WriteErrorResponse(context, ErrorCode.InternalError, message); // 返回 200 + InternalError 错误码
        }
    }

    /// <summary>
    /// 将错误信息序列化为 ApiResult JSON 并写入 HTTP 响应
    /// 固定 HTTP 200 状态码，错误信息在响应体中
    /// </summary>
    /// <param name="context">当前 HTTP 上下文</param>
    /// <param name="code">错误码（来自 ErrorCode 枚举）</param>
    /// <param name="message">错误提示信息</param>
    private static async Task WriteErrorResponse(HttpContext context, int code, string message)
    {
        context.Response.ContentType = "application/json"; // 设置响应 Content-Type 为 JSON
        // 项目约定：所有响应统一 HTTP 200，错误信息在 ApiResult body 中
        context.Response.StatusCode = (int)HttpStatusCode.OK; // 固定返回 200 OK

        var result = ApiResult.Fail(code, message); // 构造统一错误响应：{ code, message, data: null }
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase // 驼峰命名策略（前端 JS 友好）
        });
        await context.Response.WriteAsync(json); // 将 JSON 字符串写入响应体
    }
}
