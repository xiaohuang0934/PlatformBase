using Microsoft.AspNetCore.Mvc;
using PlatformBase.Application.Dtos;
using PlatformBase.Application.Services;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Host.Authorization;

namespace PlatformBase.Host.Controllers;

/// <summary>
/// 系统参数管理 API 控制器
/// 提供参数值读取（带缓存）、CRUD 管理、功能开关判断等端点
/// </summary>
[ApiController]
[Route("api/system-params")]
public class SystemParamController : ControllerBase
{
    private readonly ISystemParamService _service;

    public SystemParamController(ISystemParamService service)
    {
        _service = service;
    }

    /// <summary>获取单个参数值（公开接口，前端页面加载时使用）</summary>
    [HttpGet("{code}")]
    [Permission("system-params.list")]
    public async Task<ApiResult<string?>> GetValue(string code, CancellationToken cancellationToken)
    {
        var value = await _service.GetValueAsync(code, cancellationToken);
        return ApiResult<string?>.Ok(value);
    }

    /// <summary>获取指定分类下所有参数</summary>
    [HttpGet("category/{category}")]
    [Permission("system-params.list")]
    public async Task<ApiResult<IReadOnlyDictionary<string, string>>> GetByCategory(
        string category, CancellationToken cancellationToken)
    {
        var result = await _service.GetByCategoryAsync(category, cancellationToken);
        return ApiResult<IReadOnlyDictionary<string, string>>.Ok(result);
    }

    /// <summary>获取所有功能开关</summary>
    [HttpGet("features")]
    [Permission("system-params.list")]
    public async Task<ApiResult<IReadOnlyList<SystemParamDto>>> GetFeatures(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllFeaturesAsync(cancellationToken);
        return ApiResult<IReadOnlyList<SystemParamDto>>.Ok(result);
    }

    /// <summary>分页查询系统参数</summary>
    [HttpGet]
    [Permission("system-params.list")]
    public async Task<ApiResult<PagedResult<SystemParamDto>>> GetPaged(
        [FromQuery] SystemParamQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.GetPagedAsync(query, cancellationToken);
        return ApiResult<PagedResult<SystemParamDto>>.Ok(result);
    }

    /// <summary>查询单条参数详情</summary>
    [HttpGet("detail/{id:guid}")]
    [Permission("system-params.list")]
    public async Task<ApiResult<SystemParamDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result == null)
            return ApiResult<SystemParamDto>.Fail(ErrorCode.DataNotFound, "系统参数不存在");
        return ApiResult<SystemParamDto>.Ok(result);
    }

    /// <summary>创建系统参数</summary>
    [HttpPost]
    [Permission("system-params.create")]
    public async Task<ApiResult<SystemParamDto>> Create(
        [FromBody] CreateSystemParamDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(dto, cancellationToken);
        return ApiResult<SystemParamDto>.Ok(result);
    }

    /// <summary>更新系统参数</summary>
    [HttpPut("{id:guid}")]
    [Permission("system-params.edit")]
    public async Task<ApiResult<SystemParamDto>> Update(
        Guid id, [FromBody] UpdateSystemParamDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, dto, cancellationToken);
        return ApiResult<SystemParamDto>.Ok(result);
    }

    /// <summary>删除系统参数（软删除）</summary>
    [HttpDelete("{id:guid}")]
    [Permission("system-params.delete")]
    public async Task<ApiResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return ApiResult.Ok("删除成功");
    }
}
