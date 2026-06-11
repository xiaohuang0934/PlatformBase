using PlatformBase.Application.Services;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Repositories;

namespace PlatformBase.Host.Services.JobModule;

/// <summary>
/// 任务调度配置持久化服务实现
/// </summary>
public class JobScheduleService : IJobScheduleService
{
    private readonly IUnitOfWork _uow;

    public JobScheduleService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<JobSchedule?> GetByJobIdAsync(string jobId, CancellationToken ct = default)
    {
        return await _uow.Repository<JobSchedule>()
            .FirstOrDefaultAsync(j => j.JobId == jobId, ct);
    }

    public async Task<IReadOnlyList<JobSchedule>> GetAllAsync(CancellationToken ct = default)
    {
        return (await _uow.Repository<JobSchedule>().GetAllAsync(ct)).ToList();
    }

    public async Task UpsertAsync(string jobId, string jobName, string cronExpression,
        string? description, CancellationToken ct = default)
    {
        var existing = await GetByJobIdAsync(jobId, ct);
        if (existing != null)
        {
            existing.JobName = jobName;
            existing.CronExpression = cronExpression;
            if (description != null) existing.Description = description;
            _uow.Repository<JobSchedule>().Update(existing);
        }
        else
        {
            await _uow.Repository<JobSchedule>().AddAsync(new JobSchedule
            {
                JobId = jobId,
                JobName = jobName,
                CronExpression = cronExpression,
                Description = description,
                IsEnabled = true
            }, ct);
        }
        await _uow.SaveChangesAsync(ct);
    }

    public async Task SetEnabledAsync(string jobId, bool enabled, CancellationToken ct = default)
    {
        var existing = await GetByJobIdAsync(jobId, ct);
        if (existing == null)
            throw new BusinessException($"任务 '{jobId}' 不存在", ErrorCode.DataNotFound);

        existing.IsEnabled = enabled;
        _uow.Repository<JobSchedule>().Update(existing);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task SetCronAsync(string jobId, string cronExpression, CancellationToken ct = default)
    {
        var existing = await GetByJobIdAsync(jobId, ct);
        if (existing == null)
            throw new BusinessException($"任务 '{jobId}' 不存在", ErrorCode.DataNotFound);

        existing.CronExpression = cronExpression;
        _uow.Repository<JobSchedule>().Update(existing);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task UpdateRunResultAsync(string jobId, DateTime runAt, string? error,
        CancellationToken ct = default)
    {
        var existing = await GetByJobIdAsync(jobId, ct);
        if (existing == null) return;

        existing.LastRunAt = runAt;
        existing.LastError = error;
        _uow.Repository<JobSchedule>().Update(existing);
        await _uow.SaveChangesAsync(ct);
    }
}
