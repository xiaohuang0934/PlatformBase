using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using PlatformBase.Application.Dtos;
using PlatformBase.Application.Services;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Repositories;
using PlatformBase.Host.Authorization;

namespace PlatformBase.Host.Controllers.OperationLogModule;

/// <summary>
/// 操作日志查询 + 清理 API
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/operation-logs")]
public class OperationLogController : ControllerBase
{
    private readonly IOperationLogService _service;
    private readonly IUnitOfWork _uow;

    public OperationLogController(IOperationLogService service, IUnitOfWork uow)
    { _service = service; _uow = uow; }

    /// <summary>分页查询操作日志（按时间倒序）</summary>
    [HttpGet]
    [Permission("operation-logs.list")]
    public async Task<ApiResult<PagedResult<OperationLogDto>>> GetPaged(
        [FromQuery] OperationLogQuery query, CancellationToken ct)
    {
        var result = await _service.GetPagedAsync(query, ct);
        return ApiResult<PagedResult<OperationLogDto>>.Ok(result);
    }

    /// <summary>查看单条操作日志详情</summary>
    [HttpGet("{id:guid}")]
    [Permission("operation-logs.list")]
    public async Task<ApiResult<OperationLogDto>> GetById(Guid id, CancellationToken ct)
    {
        var entity = await _uow.Repository<OperationLog>().GetByIdAsync(id, ct);
        if (entity == null) return ApiResult<OperationLogDto>.Fail(ErrorCode.DataNotFound, "日志不存在");
        return ApiResult<OperationLogDto>.Ok(new OperationLogDto
        {
            Id = entity.Id, Username = entity.Username, Action = entity.Action,
            Resource = entity.Resource, Detail = entity.Detail, IpAddress = entity.IpAddress,
            IsSuccess = entity.IsSuccess, Timestamp = entity.Timestamp
        });
    }

    /// <summary>清理 N 天前的操作日志</summary>
    [HttpDelete("cleanup")]
    [Permission("operation-logs.list")]
    public async Task<ApiResult> Cleanup([FromQuery] int daysAgo = 90, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-daysAgo);
        var expired = await _uow.Repository<OperationLog>()
            .FindAsync(l => l.Timestamp < cutoff, ct);
        _uow.Repository<OperationLog>().DeleteRange(expired);
        await _uow.SaveChangesAsync(ct);
        return ApiResult.Ok($"已清理 {expired.Count} 条 {daysAgo} 天前的日志");
    }
}
