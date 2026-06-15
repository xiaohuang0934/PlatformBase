namespace PlatformBase.Core.Exceptions;

/// <summary>
/// 系统错误码定义
/// 所有响应统一 HTTP 200，错误信息在 ApiResult body 中
/// </summary>
public static class ErrorCode
{
    /// <summary>未知错误</summary>
    public const int Unknown = -1;

    // ========== 通用错误 ==========

    /// <summary>请求参数错误</summary>
    public const int BadRequest = 400;

    /// <summary>未认证（Token 无效或过期）</summary>
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

    /// <summary>令牌已过期</summary>
    public const int TokenExpired = 1004;

    /// <summary>令牌无效</summary>
    public const int TokenInvalid = 1005;

    /// <summary>用户不存在</summary>
    public const int UserNotFound = 1006;

    /// <summary>密码错误</summary>
    public const int PasswordMismatch = 1007;

    /// <summary>用户已被锁定</summary>
    public const int UserLocked = 1008;

    /// <summary>请求过于频繁</summary>
    public const int TooManyRequests = 1009;

    /// <summary>权限拒绝</summary>
    public const int PermissionDenied = 1010;

    // ========== 租户/部门/角色校验错误码（1011-1020） ==========

    /// <summary>必须指定租户</summary>
    public const int TenantIdRequired = 1011;

    /// <summary>无权访问指定租户</summary>
    public const int TenantAccessDenied = 1012;

    /// <summary>部门必选</summary>
    public const int OrganizationRequired = 1013;

    /// <summary>角色必选</summary>
    public const int RoleRequired = 1014;

    /// <summary>部门不属于目标租户</summary>
    public const int OrganizationNotInTenant = 1015;

    /// <summary>角色不属于目标租户</summary>
    public const int RoleNotInTenant = 1016;

    /// <summary>不能创建平台管理员</summary>
    public const int CannotCreatePlatformAdmin = 1017;

    /// <summary>只能创建租户用户</summary>
    public const int CanOnlyCreateTenantUser = 1018;

    /// <summary>父部门不属于当前租户</summary>
    public const int ParentOrgNotInTenant = 1019;

    /// <summary>无权执行此操作</summary>
    public const int NoPermissionToOperate = 1020;

    // ========== 基础设施错误码（2000 起） ==========

    /// <summary>数据库错误</summary>
    public const int DatabaseError = 2001;

    // ========== 外部服务错误码（3000 起） ==========

    /// <summary>外部服务调用失败</summary>
    public const int ExternalServiceError = 3001;
}
