using System.Net;

namespace PlatformBase.Core.Models;

/// <summary>
/// 统一API响应结果（泛型版本）
/// </summary>
/// <typeparam name="T">返回数据的类型</typeparam>
public class ApiResult<T>
{
    /// <summary>请求是否成功</summary>
    public bool Success { get; set; }

    /// <summary>状态码，200表示成功，其他为业务或系统错误码</summary>
    public int Code { get; set; }

    /// <summary>提示消息</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>响应数据</summary>
    public T? Data { get; set; }

    /// <summary>链路追踪ID，用于排查问题</summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// 创建成功响应
    /// </summary>
    /// <param name="data">返回数据</param>
    /// <param name="message">提示消息，默认为"success"</param>
    public static ApiResult<T> Ok(T data, string message = "success")
    {
        return new ApiResult<T>
        {
            Success = true,
            Code = (int)HttpStatusCode.OK,
            Message = message,
            Data = data
        };
    }

    /// <summary>
    /// 创建失败响应（自定义错误码）
    /// </summary>
    /// <param name="code">错误码，可使用ErrorCode常量</param>
    /// <param name="message">错误描述</param>
    public static ApiResult<T> Fail(int code, string message)
    {
        return new ApiResult<T>
        {
            Success = false,
            Code = code,
            Message = message
        };
    }

    /// <summary>
    /// 创建失败响应（默认400错误码）
    /// </summary>
    /// <param name="message">错误描述</param>
    public static ApiResult<T> Fail(string message)
    {
        return Fail((int)HttpStatusCode.BadRequest, message);
    }
}

/// <summary>
/// 统一API响应结果（非泛型版本，不返回数据）
/// </summary>
public class ApiResult
{
    /// <summary>请求是否成功</summary>
    public bool Success { get; set; }

    /// <summary>状态码</summary>
    public int Code { get; set; }

    /// <summary>提示消息</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>链路追踪ID</summary>
    public string? TraceId { get; set; }

    /// <summary>创建成功响应</summary>
    public static ApiResult Ok(string message = "success")
    {
        return new ApiResult
        {
            Success = true,
            Code = (int)HttpStatusCode.OK,
            Message = message
        };
    }

    /// <summary>创建失败响应（自定义错误码）</summary>
    public static ApiResult Fail(int code, string message)
    {
        return new ApiResult
        {
            Success = false,
            Code = code,
            Message = message
        };
    }

    /// <summary>创建失败响应（默认400错误码）</summary>
    public static ApiResult Fail(string message)
    {
        return Fail((int)HttpStatusCode.BadRequest, message);
    }
}
