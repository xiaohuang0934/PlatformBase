using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;

namespace PlatformBase.Host.Middleware;

/// <summary>
/// 全局异常处理中间件
/// 将业务异常和未处理异常统一转换为 ApiResult 格式，保持 HTTP 200 + 统一响应体
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
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "并发冲突：数据已被他人修改");
            await WriteErrorResponse(context, ErrorCode.Conflict, "数据已被他人修改，请刷新后重试");
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning(ex, "Business exception: {Message}", ex.Message);
            await WriteErrorResponse(context, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            var message = _env.IsDevelopment() ? ex.ToString() : "Internal server error";
            await WriteErrorResponse(context, ErrorCode.InternalError, message);
        }
    }

    /// <summary>将错误信息序列化为 ApiResult JSON 并写入 HTTP 响应</summary>
    private static async Task WriteErrorResponse(HttpContext context, int code, string message)
    {
        context.Response.ContentType = "application/json";
        // 项目约定：所有响应统一 HTTP 200，错误信息在 ApiResult body 中
        context.Response.StatusCode = (int)HttpStatusCode.OK;

        var result = ApiResult.Fail(code, message);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await context.Response.WriteAsync(json);
    }
}
