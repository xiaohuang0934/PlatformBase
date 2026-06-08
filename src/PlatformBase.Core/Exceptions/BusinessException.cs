namespace PlatformBase.Core.Exceptions;

/// <summary>
/// 业务异常，由业务逻辑主动抛出，GlobalExceptionMiddleware统一捕获为ApiResult响应
/// </summary>
public class BusinessException : Exception
{
    /// <summary>错误码，参见ErrorCode常量</summary>
    public int Code { get; }

    /// <summary>
    /// 创建业务异常
    /// </summary>
    /// <param name="message">错误描述</param>
    /// <param name="code">错误码，默认400</param>
    public BusinessException(string message, int code = ErrorCode.BadRequest)
        : base(message)
    {
        Code = code;
    }

    /// <summary>
    /// 创建包含内部异常的业务异常
    /// </summary>
    /// <param name="message">错误描述</param>
    /// <param name="code">错误码</param>
    /// <param name="innerException">内部异常</param>
    public BusinessException(string message, int code, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }
}
