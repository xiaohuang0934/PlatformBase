using Microsoft.AspNetCore.Mvc;
using PlatformBase.Application.Dtos;
using PlatformBase.Application.Services;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Host.Authorization;

namespace PlatformBase.Host.Controllers;

/// <summary>
/// 权限管理 API 控制器，提供权限点的完整 CRUD 管理
/// </summary>
[ApiController]
[Route("api/permissions")]
public class PermissionController : ControllerBase
{
    private readonly IPermissionService _service;

    public PermissionController(IPermissionService service)
    {
        _service = service;
    }

    /// <summary>分页查询权限列表</summary>
    [HttpGet]
    [Permission("perms.list")]
    public async Task<ApiResult<PagedResult<PermissionDto>>> GetPaged(
        [FromQuery] PermissionQuery query, CancellationToken ct)
    {
        var result = await _service.GetPagedAsync(query, ct);
        return ApiResult<PagedResult<PermissionDto>>.Ok(result);
    }

    /// <summary>查询权限详情</summary>
    [HttpGet("{id:guid}")]
    [Permission("perms.list")]
    public async Task<ApiResult<PermissionDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        if (result == null)
            return ApiResult<PermissionDto>.Fail(ErrorCode.DataNotFound, "权限不存在");
        return ApiResult<PermissionDto>.Ok(result);
    }

    /// <summary>创建权限点</summary>
    [HttpPost]
    [Permission("perms.list")] // 权限管理目前无独立 create/edit/delete 权限编码，复用 list
    public async Task<ApiResult<PermissionDto>> Create(
        [FromBody] CreatePermissionDto dto, CancellationToken ct)
    {
        var result = await _service.CreateAsync(dto, ct);
        return ApiResult<PermissionDto>.Ok(result);
    }

    /// <summary>更新权限点</summary>
    [HttpPut("{id:guid}")]
    [Permission("perms.list")]
    public async Task<ApiResult<PermissionDto>> Update(
        Guid id, [FromBody] UpdatePermissionDto dto, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, dto, ct);
        return ApiResult<PermissionDto>.Ok(result);
    }

    /// <summary>删除权限点（物理删除，级联清理关联）</summary>
    [HttpDelete("{id:guid}")]
    [Permission("perms.list")]
    public async Task<ApiResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return ApiResult.Ok("删除成功");
    }
}
