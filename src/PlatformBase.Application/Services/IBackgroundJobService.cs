using PlatformBase.Core.Models;

namespace PlatformBase.Application.Services;

/// <summary>
/// 后台任务调度接口，封装 Hangfire 的即时/延迟/周期性任务调度
/// 并提供启停控制、动态 Cron 修改、手动触发、状态查询等管理能力
/// </summary>
public interface IBackgroundJobService
{
    /// <summary>立即执行（火力即忘）</summary>
    void Enqueue(string jobId);

    /// <summary>启动/恢复周期性任务</summary>
    void StartRecurring(string jobId, string cronExpression);

    /// <summary>停止周期性任务</summary>
    void StopRecurring(string jobId);

    /// <summary>动态修改 Cron 表达式（仅对运行中的任务生效）</summary>
    void UpdateCron(string jobId, string newCronExpression);

    /// <summary>手动立即触发一次</summary>
    void TriggerNow(string jobId);

    /// <summary>获取所有任务状态（合并持久化配置 + Hangfire 实时状态）</summary>
    Task<IReadOnlyList<JobStatusModel>> GetAllStatusesAsync(CancellationToken cancellationToken = default);

    /// <summary>应用启动时同步：从 JobSchedules 表读取配置，注册到 Hangfire</summary>
    Task SyncFromDatabaseAsync(CancellationToken cancellationToken = default);
}
