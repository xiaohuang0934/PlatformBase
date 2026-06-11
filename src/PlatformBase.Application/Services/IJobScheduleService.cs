using PlatformBase.Core.Entities;

namespace PlatformBase.Application.Services;

/// <summary>
/// 任务调度配置持久化接口，管理 JobSchedules 表的 CRUD
/// </summary>
public interface IJobScheduleService
{
    /// <summary>根据 JobId 查询配置</summary>
    Task<JobSchedule?> GetByJobIdAsync(string jobId, CancellationToken ct = default);
    /// <summary>查询所有配置</summary>
    Task<IReadOnlyList<JobSchedule>> GetAllAsync(CancellationToken ct = default);
    /// <summary>创建或更新任务配置</summary>
    Task UpsertAsync(string jobId, string jobName, string cronExpression, string? description, CancellationToken ct = default);
    /// <summary>设置启停状态</summary>
    Task SetEnabledAsync(string jobId, bool enabled, CancellationToken ct = default);
    /// <summary>更新 Cron 表达式</summary>
    Task SetCronAsync(string jobId, string cronExpression, CancellationToken ct = default);
    /// <summary>回写执行结果（时间+错误信息）</summary>
    Task UpdateRunResultAsync(string jobId, DateTime runAt, string? error, CancellationToken ct = default);
}
