using Microsoft.AspNetCore.Authorization;          // 提供 AuthorizationHandler<T> / AuthorizationHandlerContext 基类
using PlatformBase.Application.Services;            // 提供 IPermissionService 权限查询接口
using PlatformBase.Core.Services;                   // 提供 ICurrentUserContext 当前用户信息

namespace PlatformBase.Host.Authorization;

/// <summary>
/// 权限鉴权处理器 — 执行实际的权限校验逻辑
/// 当策略包含 PermissionRequirement 时由 ASP.NET Core 鉴权框架自动调用
///
/// 实现逻辑：
///   1. 继承 AuthorizationHandler<PermissionRequirement>，框架检测到 PermissionRequirement 时自动触发
///   2. HandleRequirementAsync 每收到一个 [Permission("code")] 标记就调用一次
///   3. 校验流程：
///      a. 用户未认证 → context.Fail()，拒绝请求
///      b. 调用 IPermissionService.HasPermissionAsync(userId, permissionCode) 查询用户是否有该权限
///         - 内部优先查 Redis 缓存，MISS 时查 DB 并回写缓存
///      c. 有权限 → context.Succeed(requirement)，所有 requirement 都 Succeed 后请求通过
///      d. 无权限 → context.Fail()，拒绝请求（返回 200 + ErrorCode.Forbidden）
///      e. 查询异常 → context.Fail()，不向上抛异常，确保统一 ApiResult 响应格式
///   4. 多个 [Permission] 叠加时（AllowMultiple=true），任一通过即放行（OR 逻辑）
///      因为 AuthorizationHandlerContext 只要有一个 requirement Succeed 就判通过
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    /// <summary>权限服务，提供权限查询能力（含 Redis 缓存层）</summary>
    private readonly IPermissionService _permissionService;

    /// <summary>当前用户会话上下文（UserId / UserName / Roles 等）</summary>
    private readonly ICurrentUserContext _currentUser;

    /// <summary>结构化日志记录器</summary>
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    /// <summary>构造函数：注入权限服务、用户上下文、日志记录器</summary>
    public PermissionAuthorizationHandler(
        IPermissionService permissionService,       // 权限查询接口（含缓存）
        ICurrentUserContext currentUser,            // 当前用户会话
        ILogger<PermissionAuthorizationHandler> logger) // 日志记录器
    {
        _permissionService = permissionService; // 保存权限服务引用
        _currentUser = currentUser;             // 保存用户上下文引用
        _logger = logger;                       // 保存日志记录器引用
    }

    /// <summary>
    /// 权限校验入口：验证当前用户是否拥有指定权限编码
    /// 框架每遇到一个 [Permission("code")] 标记就调用一次此方法
    /// </summary>
    /// <param name="context">授权处理上下文（通过 Succeed/Fail 控制结果）</param>
    /// <param name="requirement">权限要求（含 PermissionCode）</param>
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,           // 框架提供的授权上下文
        PermissionRequirement requirement)             // PermissionPolicyProvider 动态创建的权限要求
    {
        // 守卫：用户未认证或无法识别 → 直接拒绝
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == null)
        {
            _logger.LogDebug("权限鉴权失败：用户未认证，请求权限={Permission}", requirement.PermissionCode); // 记录调试日志
            context.Fail(); // 标记鉴权失败（Http 响应由 JwtBearerEvents.OnForbidden 转为 ApiResult）
            return;
        }

        // 查询用户权限集合（优先 Redis 缓存，缓存未命中查库回写）
        // 异常时标记鉴权失败，不向上抛，确保统一响应格式
        try
        {
            var hasPermission = await _permissionService.HasPermissionAsync(
                _currentUser.UserId.Value,          // 当前用户 ID
                requirement.PermissionCode);        // 待校验的权限编码

            if (hasPermission) // 用户拥有该权限
            {
                _logger.LogDebug("权限鉴权通过：用户={User} 权限={Permission}", // 记录调试日志
                    _currentUser.UserName, requirement.PermissionCode);
                context.Succeed(requirement); // 标记当前 requirement 已满足
            }
            else // 用户无该权限
            {
                _logger.LogWarning("权限鉴权拒绝：用户={User} 权限={Permission}", // 记录警告日志
                    _currentUser.UserName, requirement.PermissionCode);
                context.Fail(); // 标记鉴权失败
            }
        }
        catch (Exception ex) // 权限查询异常（Redis 不可用且 DB 也失败等极端情况）
        {
            _logger.LogError(ex, "权限鉴权异常：用户={User} 权限={Permission}", // 记录错误日志
                _currentUser.UserName, requirement.PermissionCode);
            context.Fail(); // 异常时拒绝请求，保证安全性（不因服务异常而放行）
        }
    }
}
