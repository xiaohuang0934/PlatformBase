namespace PlatformBase.Core.Models;

/// <summary>
/// 任务运行状态 DTO，合并 JobSchedules 表持久化配置与 Hangfire 实时状态
/// </summary>
public class JobStatusModel
{
    /// <summary>任务唯一标识</summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>任务显示名称</summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>Cron 表达式</summary>
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; }

    /// <summary>是否正在执行</summary>
    public bool IsRunning { get; set; }

    /// <summary>上次执行时间</summary>
    public DateTime? LastRunAt { get; set; }

    /// <summary>最近错误信息</summary>
    public string? LastError { get; set; }

    /// <summary>预计下次执行时间</summary>
    public DateTime? NextRunAt { get; set; }

    /// <summary>任务说明</summary>
    public string? Description { get; set; }
}
