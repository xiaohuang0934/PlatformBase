namespace PlatformBase.Core.Exceptions;

/// <summary>
/// 系统错误码定义
/// </summary>
public static class ErrorCode
{
    /// <summary>未知错误</summary>
    public const int Unknown = -1;

    /// <summary>请求参数错误</summary>
    public const int BadRequest = 400;

    /// <summary>未认证</summary>
    public const int Unauthorized = 401;

    /// <summary>无权限访问</summary>
    public const int Forbidden = 403;

    /// <summary>资源不存在</summary>
    public const int NotFound = 404;

    /// <summary>数据冲突</summary>
    public const int Conflict = 409;

    /// <summary>模型验证失败</summary>
    public const int ValidationFailed = 422;

    /// <summary>服务器内部错误</summary>
    public const int InternalError = 500;

    // ========== 业务错误码（1000 起） ==========

    /// <summary>重复记录</summary>
    public const int DuplicateRecord = 1001;

    /// <summary>数据不存在</summary>
    public const int DataNotFound = 1002;

    /// <summary>非法操作</summary>
    public const int InvalidOperation = 1003;

    // ========== 基础设施错误码（2000 起） ==========

    /// <summary>数据库错误</summary>
    public const int DatabaseError = 2001;

    // ========== 外部服务错误码（3000 起） ==========

    /// <summary>外部服务调用失败</summary>
    public const int ExternalServiceError = 3001;
}
