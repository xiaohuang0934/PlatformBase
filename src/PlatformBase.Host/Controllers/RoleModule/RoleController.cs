using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using PlatformBase.Application.Dtos;
using PlatformBase.Application.Services;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Host.Authorization;
using PlatformBase.Host.Filters;

namespace PlatformBase.Host.Controllers.RoleModule;

/// <summary>
/// 角色管理 API 控制器，提供角色的完整 CRUD + 权限分配/查询
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/roles")]
public class RoleController : ControllerBase
{
    private readonly IRoleService _service;

    public RoleController(IRoleService service)
    {
        _service = service;
    }

    /// <summary>分页查询角色列表</summary>
    [HttpGet]
    [Permission("roles.list")]
    public async Task<ApiResult<PagedResult<RoleDto>>> GetPaged(
        [FromQuery] RoleQuery query, CancellationToken ct)
    {
        var result = await _service.GetPagedAsync(query, ct);
        return ApiResult<PagedResult<RoleDto>>.Ok(result);
    }

    /// <summary>查询角色详情</summary>
    [HttpGet("{id:guid}")]
    [Permission("roles.list")]
    public async Task<ApiResult<RoleDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        if (result == null)
            return ApiResult<RoleDto>.Fail(ErrorCode.DataNotFound, "角色不存在");
        return ApiResult<RoleDto>.Ok(result);
    }

    /// <summary>创建角色</summary>
    [HttpPost]
    [Permission("roles.create")]
    [OperationLog("create", Resource = "Role")]
    public async Task<ApiResult<RoleDto>> Create(
        [FromBody] CreateRoleDto dto, CancellationToken ct)
    {
        var result = await _service.CreateAsync(dto, ct);
        return ApiResult<RoleDto>.Ok(result);
    }

    /// <summary>更新角色</summary>
    [HttpPut("{id:guid}")]
    [Permission("roles.edit")]
    [OperationLog("update", Resource = "Role")]
    public async Task<ApiResult<RoleDto>> Update(
        Guid id, [FromBody] UpdateRoleDto dto, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, dto, ct);
        return ApiResult<RoleDto>.Ok(result);
    }

    /// <summary>删除角色（需先确保无用户关联）</summary>
    [HttpDelete("{id:guid}")]
    [Permission("roles.delete")]
    [OperationLog("delete", Resource = "Role")]
    public async Task<ApiResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return ApiResult.Ok("删除成功");
    }

    /// <summary>获取角色拥有的权限编码列表</summary>
    [HttpGet("{id:guid}/permissions")]
    [Permission("roles.list")]
    public async Task<ApiResult<IReadOnlyList<string>>> GetPermissions(Guid id, CancellationToken ct)
    {
        var result = await _service.GetPermissionCodesAsync(id, ct);
        return ApiResult<IReadOnlyList<string>>.Ok(result);
    }

    /// <summary>给角色批量分配权限（全量替换）</summary>
    [HttpPut("{id:guid}/permissions")]
    [Permission("roles.edit")]
    [OperationLog("update", Resource = "Role:Permissions")]
    public async Task<ApiResult> AssignPermissions(
        Guid id, [FromBody] IReadOnlyList<string> permissionCodes, CancellationToken ct)
    {
        await _service.AssignPermissionsAsync(id, permissionCodes, ct);
        return ApiResult.Ok("权限分配成功");
    }
}
