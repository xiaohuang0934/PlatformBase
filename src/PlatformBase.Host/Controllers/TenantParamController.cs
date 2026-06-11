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

namespace PlatformBase.Host.Controllers;

/// <summary>
/// 租户参数管理 API
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/tenant-params")]
public class TenantParamController : ControllerBase
{
    private readonly ISystemParamService _spService;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public TenantParamController(ISystemParamService spService, IUnitOfWork uow, ICurrentUserService currentUser)
    { _spService = spService; _uow = uow; _currentUser = currentUser; }

    /// <summary>查询当前租户的参数覆盖值</summary>
    [HttpGet("{code}")]
    [Permission("tenant-params.list")]
    public async Task<ApiResult<string?>> GetValue(string code, CancellationToken ct)
    {
        var tid = _currentUser.TenantId;
        if (tid == null) return ApiResult<string?>.Fail(ErrorCode.BadRequest, "请先选择租户");
        var value = await _spService.GetValueAsync(code, ct);
        return ApiResult<string?>.Ok(value);
    }

    /// <summary>创建租户参数覆盖值</summary>
    [HttpPost]
    [Permission("tenant-params.create")]
    public async Task<ApiResult> Create([FromBody] CreateSystemParamDto dto, CancellationToken ct)
    {
        var tid = _currentUser.TenantId;
        if (tid == null) return ApiResult.Fail(ErrorCode.BadRequest, "请先选择租户");
        var exists = await _uow.Repository<TenantParam>()
            .AnyAsync(p => p.TenantId == tid.Value && p.Code == dto.Code, ct);
        if (exists) throw new BusinessException("参数编码已存在", ErrorCode.DuplicateRecord);

        await _uow.Repository<TenantParam>().AddAsync(new TenantParam
        {
            TenantId = tid.Value, Code = dto.Code, Name = dto.Name, Value = dto.Value,
            Category = dto.Category, Description = dto.Description, SortOrder = dto.SortOrder
        }, ct);
        await _uow.SaveChangesAsync(ct);
        await _spService.InvalidateCacheAsync(dto.Code, ct);
        return ApiResult.Ok("创建成功");
    }

    /// <summary>更新租户参数覆盖值</summary>
    [HttpPut("{id:guid}")]
    [Permission("tenant-params.edit")]
    public async Task<ApiResult> Update(Guid id, [FromBody] UpdateSystemParamDto dto, CancellationToken ct)
    {
        var p = await _uow.Repository<TenantParam>().GetByIdAsync(id, ct);
        if (p == null) return ApiResult.Fail(ErrorCode.DataNotFound, "参数不存在");
        if (dto.Value != null) p.Value = dto.Value;
        _uow.Repository<TenantParam>().Update(p);
        await _uow.SaveChangesAsync(ct);
        await _spService.InvalidateCacheAsync(p.Code, ct);
        return ApiResult.Ok("更新成功");
    }
}
