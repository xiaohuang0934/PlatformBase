using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformBase.Application.Dtos;
using PlatformBase.Application.Services;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Services;

namespace PlatformBase.Host.Controllers;

/// <summary>
/// 认证授权 API 控制器，提供登录、Token 刷新、用户资料、密码修改、权限列表等端点
/// 所有业务逻辑委托给 IAuthService / IUserService / IPermissionService
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserService _currentUser;

    public AuthController(
        IAuthService authService,
        IUserService userService,
        IPermissionService permissionService,
        ICurrentUserService currentUser)
    {
        _authService = authService;
        _userService = userService;
        _permissionService = permissionService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 用户登录，返回 AccessToken + RefreshToken
    /// 业务逻辑委托给 IAuthService.LoginAsync，异常由 GlobalExceptionMiddleware 统一处理
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ApiResult<LoginResponse>> Login(
        [FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return ApiResult<LoginResponse>.Ok(result);
    }

    /// <summary>
    /// 刷新 Token：用 RefreshToken 换新的 AccessToken + RefreshToken（旧 RT 一次性消费）
    /// 业务逻辑委托给 IAuthService.RefreshTokenAsync
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ApiResult<LoginResponse>> Refresh(
        [FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshTokenAsync(request.RefreshToken, cancellationToken);
        return ApiResult<LoginResponse>.Ok(result);
    }

    /// <summary>
    /// 获取当前登录用户的资料信息
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    public async Task<ApiResult<UserProfileDto>> GetProfile(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId == null)
            return ApiResult<UserProfileDto>.Fail(ErrorCode.Unauthorized, "未登录");

        var user = await _userService.GetByIdAsync(_currentUser.UserId.Value, cancellationToken);
        if (user == null)
            return ApiResult<UserProfileDto>.Fail(ErrorCode.UserNotFound, "用户不存在");

        var roles = await _userService.GetRolesAsync(user.Id, cancellationToken);

        return ApiResult<UserProfileDto>.Ok(new UserProfileDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            Roles = roles,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        });
    }

    /// <summary>
    /// 修改当前用户的登录密码
    /// 业务逻辑委托给 IAuthService.ChangePasswordAsync（含 BCrypt 哈希、Token 撤销）
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ApiResult> ChangePassword(
        [FromBody] ChangePasswordDto dto, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId == null)
            return ApiResult.Fail(ErrorCode.Unauthorized, "未登录");

        await _authService.ChangePasswordAsync(
            _currentUser.UserId.Value, dto.CurrentPassword, dto.NewPassword, cancellationToken);
        return ApiResult.Ok("密码修改成功");
    }

    /// <summary>
    /// 获取当前用户的所有权限编码列表
    /// </summary>
    [HttpGet("permissions")]
    [Authorize]
    public async Task<ApiResult<IReadOnlyList<string>>> GetPermissions(
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId == null)
            return ApiResult<IReadOnlyList<string>>.Fail(ErrorCode.Unauthorized, "未登录");

        var codes = await _permissionService.GetUserPermissionCodesAsync(
            _currentUser.UserId.Value, cancellationToken);

        return ApiResult<IReadOnlyList<string>>.Ok(codes);
    }
}
