using PlatformBase.Core.Entities;

namespace PlatformBase.Application.Services;

/// <summary>
/// 任务调度配置持久化接口，管理 JobSchedules 表的 CRUD
/// </summary>
public interface IJobScheduleService
{
    Task<JobSchedule?> GetByJobIdAsync(string jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobSchedule>> GetAllAsync(CancellationToken cancellationToken = default);
    Task UpsertAsync(string jobId, string jobName, string cronExpression,
        string? description, CancellationToken cancellationToken = default);
    Task SetEnabledAsync(string jobId, bool enabled, CancellationToken cancellationToken = default);
    Task SetCronAsync(string jobId, string cronExpression, CancellationToken cancellationToken = default);
    Task UpdateRunResultAsync(string jobId, DateTime runAt, string? error,
        CancellationToken cancellationToken = default);
}
