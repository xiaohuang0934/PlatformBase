namespace PlatformBase.Core.Entities;

/// <summary>
/// 任务调度配置实体，持久化存储周期性任务的 Cron 表达式和启停状态
/// 继承 <see cref="AuditableEntity"/> 获得审计追踪（不需要软删除，用 IsEnabled 控制启停）
/// </summary>
public class JobSchedule : AuditableEntity
{
    /// <summary>任务唯一标识（如 cleanup-expired-tokens）</summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>任务显示名称（如 "过期Token清理"）</summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>Cron 表达式（6 位：秒 分 时 日 月 周）</summary>
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>是否启用（false = 已停止）</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>任务说明</summary>
    public string? Description { get; set; }

    /// <summary>上次执行时间（从 Hangfire MonitoringApi 回写）</summary>
    public DateTime? LastRunAt { get; set; }

    /// <summary>最近错误信息（从 Hangfire 回写）</summary>
    public string? LastError { get; set; }
}
