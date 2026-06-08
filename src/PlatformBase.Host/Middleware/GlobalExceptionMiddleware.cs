using System.Net;
using System.Text.Json;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;

namespace PlatformBase.Host.Middleware;

/// <summary>
/// 全局异常处理中间件
/// 捕获未处理异常，统一转换为ApiResult格式返回，避免敏感信息泄露
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BusinessException ex)
        {
            // 业务异常：记录警告级别日志，返回业务错误码
            _logger.LogWarning(ex, "Business exception: {Message}", ex.Message);
            await WriteErrorResponse(context, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            // 未处理异常：记录错误级别日志，开发环境返回完整堆栈，生产环境返回通用错误
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            var message = _env.IsDevelopment() ? ex.ToString() : "Internal server error";
            await WriteErrorResponse(context, ErrorCode.InternalError, message);
        }
    }

    /// <summary>将错误信息序列化为ApiResult JSON并写入HTTP响应</summary>
    private static async Task WriteErrorResponse(HttpContext context, int code, string message)
    {
        context.Response.ContentType = "application/json";
        // 服务器错误（5xx）设置HTTP状态码为500，业务错误保持HTTP 200
        context.Response.StatusCode = code >= 500 ? (int)HttpStatusCode.InternalServerError : (int)HttpStatusCode.OK;

        var result = ApiResult.Fail(code, message);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await context.Response.WriteAsync(json);
    }
}
