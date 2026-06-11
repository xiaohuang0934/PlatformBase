using PlatformBase.Application.Dtos;
using PlatformBase.Core.Models;

namespace PlatformBase.Application.Services;

/// <summary>
/// 操作日志服务接口
/// 提供分页查询（只读）和异步写入（Hangfire 消费）
/// </summary>
public interface IOperationLogService
{
    /// <summary>分页查询操作日志（按时间倒序）</summary>
    Task<PagedResult<OperationLogDto>> GetPagedAsync(OperationLogQuery query, CancellationToken ct = default);

    /// <summary>异步写入操作日志（由 Hangfire Job 消费）</summary>
    Task WriteAsync(OperationLogEntry entry, CancellationToken ct = default);
}
