using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using PlatformBase.Application.Dtos;
using PlatformBase.Application.Services;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Repositories;
using PlatformBase.Core.Services;
using PlatformBase.Host.Authorization;
using PlatformBase.Host.Filters;

namespace PlatformBase.Host.Controllers.UserModule;

/// <summary>
/// 用户管理 API 控制器，提供用户的完整 CRUD + 角色分配 + 启用/禁用 + 密码重置
/// 认证相关端点（login/profile/change-password）在 AuthController 中
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/users")]
public class UserController : ControllerBase
{
    private readonly IUserService _service;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _currentUser;

    public UserController(IUserService service, IUnitOfWork uow, ICurrentUserContext currentUser)
    {
        _service = service;
        _uow = uow;
        _currentUser = currentUser;
    }

    /// <summary>分页查询用户列表</summary>
    [HttpGet]
    [Permission("users.list")]
    public async Task<ApiResult<PagedResult<UserDto>>> GetPaged(
        [FromQuery] UserQuery query, CancellationToken ct)
    {
        var result = await _service.GetPagedAsync(query, ct);
        return ApiResult<PagedResult<UserDto>>.Ok(result);
    }

    /// <summary>查询用户详情（含角色列表）</summary>
    [HttpGet("{id:guid}")]
    [Permission("users.list")]
    public async Task<ApiResult<UserDto>> GetById(Guid id, CancellationToken ct)
    {
        var user = await _service.GetByIdAsync(id, ct);
        if (user == null)
            return ApiResult<UserDto>.Fail(ErrorCode.UserNotFound, "用户不存在");

        var roles = await _service.GetRolesAsync(user.Id, ct);

        return ApiResult<UserDto>.Ok(new UserDto
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

    /// <summary>创建用户</summary>
    [HttpPost]
    [Permission("users.create")]
    [OperationLog("create", Resource = "User")]
    public async Task<ApiResult<UserDto>> Create(
        [FromBody] CreateUserDto dto, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var user = new User
            {
                Username = dto.Username,
                NormalizedUsername = dto.Username.ToUpperInvariant(),
                Email = dto.Email,
                NormalizedEmail = dto.Email?.ToUpperInvariant(),
                PhoneNumber = dto.PhoneNumber,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                IsActive = true,
                TenantId = _currentUser.CurrentTenantId,
                UserType = _currentUser.IsSuperAdmin && _currentUser.CurrentTenantId == null
                    ? UserType.PlatformAdmin : UserType.TenantUser
            };

            var created = await _service.CreateAsync(user, ct);

            if (dto.RoleIds?.Count > 0)
            {
                foreach (var roleId in dto.RoleIds)
                    await _service.AddToRoleAsync(created.Id, roleId, ct);
            }

            await _uow.CommitTransactionAsync(ct);
            var roles = await _service.GetRolesAsync(created.Id, ct);

            return ApiResult<UserDto>.Ok(new UserDto
            {
                Id = created.Id,
                Username = created.Username,
                Email = created.Email,
                EmailConfirmed = created.EmailConfirmed,
                PhoneNumber = created.PhoneNumber,
                IsActive = created.IsActive,
                Roles = roles,
                CreatedAt = created.CreatedAt,
                UpdatedAt = created.UpdatedAt
            });
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }
    }

    /// <summary>更新用户基本信息</summary>
    [HttpPut("{id:guid}")]
    [Permission("users.edit")]
    [OperationLog("update", Resource = "User")]
    public async Task<ApiResult<UserDto>> Update(
        Guid id, [FromBody] UpdateUserDto dto, CancellationToken ct)
    {
        var user = await _service.GetByIdAsync(id, ct);
        if (user == null)
            return ApiResult<UserDto>.Fail(ErrorCode.UserNotFound, "用户不存在");

        if (dto.Email != null)
        {
            user.Email = dto.Email;
            user.NormalizedEmail = dto.Email.ToUpperInvariant();
        }
        if (dto.PhoneNumber != null)
            user.PhoneNumber = dto.PhoneNumber;

        await _service.UpdateAsync(user, ct);

        var roles = await _service.GetRolesAsync(user.Id, ct);
        return ApiResult<UserDto>.Ok(new UserDto
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

    /// <summary>删除用户（软删除）</summary>
    [HttpDelete("{id:guid}")]
    [Permission("users.delete")]
    [OperationLog("delete", Resource = "User")]
    public async Task<ApiResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.SoftDeleteAsync(id, ct);
        return ApiResult.Ok("删除成功");
    }

    /// <summary>启用/禁用用户</summary>
    [HttpPatch("{id:guid}/toggle")]
    [Permission("users.edit")]
    public async Task<ApiResult> Toggle(Guid id, CancellationToken ct)
    {
        var user = await _service.GetByIdAsync(id, ct);
        if (user == null)
            return ApiResult.Fail(ErrorCode.UserNotFound, "用户不存在");

        var newActive = !user.IsActive; // 先保存目标状态，避免 EF 变更追踪覆盖
        await _service.SetActiveAsync(id, newActive, ct);
        return ApiResult.Ok(newActive ? "用户已启用" : "用户已禁用");
    }

    /// <summary>重置用户密码（管理员操作）</summary>
    [HttpPost("{id:guid}/reset-password")]
    [Permission("users.edit")]
    public async Task<ApiResult> ResetPassword(
        Guid id, [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await _service.ResetPasswordAsync(id, request.NewPassword, ct);
        return ApiResult.Ok("密码已重置");
    }

    // ═══════════════════ 用户-角色关联管理 ═══════════════════

    /// <summary>获取用户的角色列表</summary>
    [HttpGet("{id:guid}/roles")]
    [Permission("users.list")]
    public async Task<ApiResult<IReadOnlyList<string>>> GetRoles(Guid id, CancellationToken ct)
    {
        var roles = await _service.GetRolesAsync(id, ct);
        return ApiResult<IReadOnlyList<string>>.Ok(roles);
    }

    /// <summary>给用户批量分配角色（全量替换）</summary>
    [HttpPut("{id:guid}/roles")]
    [Permission("users.edit")]
    public async Task<ApiResult> AssignRoles(
        Guid id, [FromBody] IReadOnlyList<Guid> roleIds, CancellationToken ct)
    {
        var user = await _service.GetByIdAsync(id, ct);
        if (user == null)
            return ApiResult.Fail(ErrorCode.UserNotFound, "用户不存在");

        await _uow.BeginTransactionAsync(ct);
        try
        {
            // 全量替换角色：先移除现有角色，再分配新角色
            await _service.ClearRolesAsync(id, ct);
            foreach (var roleId in roleIds)
                await _service.AddToRoleAsync(id, roleId, ct);

            await _uow.CommitTransactionAsync(ct);
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }

        return ApiResult.Ok("角色分配成功");
    }
}
