using PlatformBase.Application.Services;
using PlatformBase.Core.Models;

namespace PlatformBase.Host.Jobs;

/// <summary>
/// 操作日志写入任务，由 Hangfire Enqueue 消费，异步写入数据库不阻塞 HTTP 请求
/// </summary>
public class OperationLogWriterJob
{
    private readonly IOperationLogService _logService;

    public OperationLogWriterJob(IOperationLogService logService)
    {
        _logService = logService;
    }

    /// <summary>写入单条操作日志到数据库</summary>
    [Hangfire.AutomaticRetry(Attempts = 0)]
    public async Task WriteAsync(OperationLogEntry entry, CancellationToken ct)
    {
        await _logService.WriteAsync(entry, ct);
    }
}
