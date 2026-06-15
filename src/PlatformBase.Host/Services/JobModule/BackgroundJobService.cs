using Hangfire;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Host.Jobs;

namespace PlatformBase.Host.Services.JobModule;

/// <summary>
/// 后台任务调度服务实现，封装 Hangfire 调度能力
/// 管理周期性任务的启停、动态 Cron、手动触发和状态聚合
/// </summary>
public class BackgroundJobService : IBackgroundJobService
{
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly IJobScheduleService _scheduleService;
    private readonly IServiceProvider _serviceProvider;

    public BackgroundJobService(
        IRecurringJobManager recurringJobManager,
        IJobScheduleService scheduleService,
        IServiceProvider serviceProvider)
    {
        _recurringJobManager = recurringJobManager;
        _scheduleService = scheduleService;
        _serviceProvider = serviceProvider;
    }

    public void Enqueue(string jobId)
    {
        var definition = JobRegistry.Find(jobId);
        if (definition == null)
            throw new BusinessException($"未找到任务定义: {jobId}", ErrorCode.DataNotFound);

        BackgroundJob.Enqueue(() => ExecuteJobAsync(definition.JobType, CancellationToken.None));
    }

    public void StartRecurring(string jobId, string cronExpression)
    {
        var definition = JobRegistry.Find(jobId);
        if (definition == null)
            throw new BusinessException($"未找到任务定义: {jobId}", ErrorCode.DataNotFound);

        _recurringJobManager.AddOrUpdate(
            jobId,
            () => ExecuteJobAsync(definition.JobType, CancellationToken.None),
            cronExpression);
    }

    public void StopRecurring(string jobId)
    {
        _recurringJobManager.RemoveIfExists(jobId);
    }

    public void UpdateCron(string jobId, string newCronExpression)
    {
        var definition = JobRegistry.Find(jobId);
        if (definition == null)
            throw new BusinessException($"未找到任务定义: {jobId}", ErrorCode.DataNotFound);

        _recurringJobManager.AddOrUpdate(
            jobId,
            () => ExecuteJobAsync(definition.JobType, CancellationToken.None),
            newCronExpression);
    }

    public void TriggerNow(string jobId)
    {
        var definition = JobRegistry.Find(jobId);
        if (definition == null)
            throw new BusinessException($"未找到任务定义: {jobId}", ErrorCode.DataNotFound);

        _recurringJobManager.Trigger(jobId);
    }

    public async Task<IReadOnlyList<JobStatusModel>> GetAllStatusesAsync(CancellationToken ct = default)
    {
        var schedules = await _scheduleService.GetAllAsync(ct);
        var monitoring = JobStorage.Current.GetMonitoringApi();

        var runningJobs = monitoring.ProcessingJobs(0, 1000);
        var runningJobIds = new HashSet<string>(
            runningJobs.Where(j => j.Value?.Job != null)
                .Select(j => j.Value.Job.Args.FirstOrDefault()?.ToString() ?? string.Empty));

        var result = new List<JobStatusModel>();
        foreach (var schedule in schedules)
        {
            var nextRun = monitoring.ScheduledJobs(0, 1000)
                .FirstOrDefault(j =>
                {
                    var args = j.Value?.Job?.Args;
                    return args != null && args.Count > 0 && args[0]?.ToString() == schedule.JobId;
                });

            result.Add(new JobStatusModel
            {
                JobId = schedule.JobId,
                JobName = schedule.JobName,
                CronExpression = schedule.CronExpression,
                IsEnabled = schedule.IsEnabled,
                IsRunning = runningJobIds.Contains(schedule.JobId),
                LastRunAt = schedule.LastRunAt,
                LastError = schedule.LastError,
                NextRunAt = nextRun.Value?.EnqueueAt,
                Description = schedule.Description
            });
        }

        return result;
    }

    /// <summary>应用启动时从数据库同步配置到 Hangfire</summary>
    public async Task SyncFromDatabaseAsync(CancellationToken ct = default)
    {
        // ① 注册所有声明的 Job（JobRegistry 中的默认配置）
        foreach (var definition in JobRegistry.AllJobs)
        {
            var schedule = await _scheduleService.GetByJobIdAsync(definition.JobId, ct);

            if (schedule == null)
            {
                // 数据库中不存在 → 用默认配置写入
                await _scheduleService.UpsertAsync(
                    definition.JobId, definition.JobName,
                    definition.DefaultCron, definition.Description, ct);
                schedule = new JobSchedule
                {
                    JobId = definition.JobId,
                    CronExpression = definition.DefaultCron,
                    IsEnabled = true
                };
            }

            // ② 对启用的任务注册到 Hangfire
            if (schedule.IsEnabled)
            {
                StartRecurring(schedule.JobId, schedule.CronExpression);
            }
        }
    }

    /// <summary>通过 IServiceProvider 解析 Job 并执行</summary>
    [AutomaticRetry(Attempts = 0)] // 失败不重试，避免级联
    public async Task ExecuteJobAsync(Type jobType, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var job = (IRecurringJob)scope.ServiceProvider.GetRequiredService(jobType);
        await job.ExecuteAsync(ct);
    }
}
