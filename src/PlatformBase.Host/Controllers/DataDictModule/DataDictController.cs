using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformBase.Application.Dtos;
using PlatformBase.Application.Services;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Host.Authorization;

namespace PlatformBase.Host.Controllers.DataDictModule;

/// <summary>
/// 数据字典管理 API 控制器
/// 提供字典类型/项的 CRUD 管理及高性能快查端点
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/data-dict")]
public class DataDictController : ControllerBase
{
    private readonly IDataDictService _service;

    public DataDictController(IDataDictService service)
    {
        _service = service;
    }

    // ═══════════════════ 字典类型 ═══════════════════

    /// <summary>分页查询字典类型</summary>
    [HttpGet("types")]
    [Permission("datadict.list")]
    public async Task<ApiResult<PagedResult<DataDictTypeDto>>> GetTypes(
        [FromQuery] DataDictTypeQuery query, CancellationToken ct)
    {
        var result = await _service.GetTypesAsync(query, ct);
        return ApiResult<PagedResult<DataDictTypeDto>>.Ok(result);
    }

    /// <summary>查询字典类型详情</summary>
    [HttpGet("types/{id:guid}")]
    [Permission("datadict.list")]
    public async Task<ApiResult<DataDictTypeDto>> GetTypeById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetTypeByIdAsync(id, ct);
        if (result == null)
            return ApiResult<DataDictTypeDto>.Fail(ErrorCode.DataNotFound, "字典类型不存在");
        return ApiResult<DataDictTypeDto>.Ok(result);
    }

    /// <summary>创建字典类型</summary>
    [HttpPost("types")]
    [Permission("datadict.create")]
    public async Task<ApiResult<DataDictTypeDto>> CreateType(
        [FromBody] CreateDataDictTypeDto dto, CancellationToken ct)
    {
        var result = await _service.CreateTypeAsync(dto, ct);
        return ApiResult<DataDictTypeDto>.Ok(result);
    }

    /// <summary>更新字典类型</summary>
    [HttpPut("types/{id:guid}")]
    [Permission("datadict.edit")]
    public async Task<ApiResult<DataDictTypeDto>> UpdateType(
        Guid id, [FromBody] UpdateDataDictTypeDto dto, CancellationToken ct)
    {
        var result = await _service.UpdateTypeAsync(id, dto, ct);
        return ApiResult<DataDictTypeDto>.Ok(result);
    }

    /// <summary>删除字典类型（级联删除所有项）</summary>
    [HttpDelete("types/{id:guid}")]
    [Permission("datadict.delete")]
    public async Task<ApiResult> DeleteType(Guid id, CancellationToken ct)
    {
        await _service.DeleteTypeAsync(id, ct);
        return ApiResult.Ok("删除成功");
    }

    // ═══════════════════ 字典项 ═══════════════════

    /// <summary>查询某个类型下的所有字典项（树形结构）</summary>
    [HttpGet("types/{dictTypeId:guid}/items")]
    [Permission("datadict.list")]
    public async Task<ApiResult<IReadOnlyList<DataDictItemDto>>> GetItems(
        Guid dictTypeId, CancellationToken ct)
    {
        var result = await _service.GetItemsAsync(dictTypeId, ct);
        return ApiResult<IReadOnlyList<DataDictItemDto>>.Ok(result);
    }

    /// <summary>查询字典项详情</summary>
    [HttpGet("items/{id:guid}")]
    [Permission("datadict.list")]
    public async Task<ApiResult<DataDictItemDto>> GetItemById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetItemByIdAsync(id, ct);
        if (result == null)
            return ApiResult<DataDictItemDto>.Fail(ErrorCode.DataNotFound, "字典项不存在");
        return ApiResult<DataDictItemDto>.Ok(result);
    }

    /// <summary>创建字典项</summary>
    [HttpPost("items")]
    [Permission("datadict.create")]
    public async Task<ApiResult<DataDictItemDto>> CreateItem(
        [FromBody] CreateDataDictItemDto dto, CancellationToken ct)
    {
        var result = await _service.CreateItemAsync(dto, ct);
        return ApiResult<DataDictItemDto>.Ok(result);
    }

    /// <summary>更新字典项</summary>
    [HttpPut("items/{id:guid}")]
    [Permission("datadict.edit")]
    public async Task<ApiResult<DataDictItemDto>> UpdateItem(
        Guid id, [FromBody] UpdateDataDictItemDto dto, CancellationToken ct)
    {
        var result = await _service.UpdateItemAsync(id, dto, ct);
        return ApiResult<DataDictItemDto>.Ok(result);
    }

    /// <summary>删除字典项</summary>
    [HttpDelete("items/{id:guid}")]
    [Permission("datadict.delete")]
    public async Task<ApiResult> DeleteItem(Guid id, CancellationToken ct)
    {
        await _service.DeleteItemAsync(id, ct);
        return ApiResult.Ok("删除成功");
    }

    // ═══════════════════ 快查接口 ═══════════════════

    /// <summary>按类型编码获取字典项（公开接口，带缓存，树形结构）</summary>
    [HttpGet("code/{typeCode}")]
    [AllowAnonymous]
    public async Task<ApiResult<IReadOnlyList<DataDictItemDto>>> GetByTypeCode(
        string typeCode, CancellationToken ct)
    {
        var result = await _service.GetItemsByTypeCodeAsync(typeCode, ct);
        return ApiResult<IReadOnlyList<DataDictItemDto>>.Ok(result);
    }

    /// <summary>批量获取多组字典（公开接口，减少网络往返）</summary>
    [HttpGet("codes")]
    [AllowAnonymous]
    public async Task<ApiResult<IReadOnlyDictionary<string, IReadOnlyList<DataDictItemDto>>>> GetByTypeCodes(
        [FromQuery] string[] codes, CancellationToken ct)
    {
        var result = await _service.GetItemsByTypeCodesAsync(codes, ct);
        return ApiResult<IReadOnlyDictionary<string, IReadOnlyList<DataDictItemDto>>>.Ok(result);
    }
}
