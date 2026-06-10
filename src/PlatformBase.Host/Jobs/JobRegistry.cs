using PlatformBase.Application.Services;

namespace PlatformBase.Host.Jobs;

/// <summary>
/// 任务注册表，声明所有可用的后台任务定义及其默认配置
/// 新增任务只需在此添加一条定义 + 实现 IRecurringJob 接口即可
/// </summary>
public static class JobRegistry
{
    public static readonly IReadOnlyList<JobDefinition> AllJobs = new List<JobDefinition>
    {
        new()
        {
            JobId = "cleanup-expired-tokens",
            JobName = "过期Token清理",
            DefaultCron = "0 3 * * *", // 每天凌晨 3 点
            Description = "清理 30 天前过期的 RefreshToken 记录",
            JobType = typeof(CleanupExpiredTokensJob)
        }
    };

    public static JobDefinition? Find(string jobId) =>
        AllJobs.FirstOrDefault(j => j.JobId == jobId);
}

/// <summary>
/// 任务定义元数据
/// </summary>
public class JobDefinition
{
    public string JobId { get; init; } = string.Empty;
    public string JobName { get; init; } = string.Empty;
    public string DefaultCron { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Type JobType { get; init; } = null!;
}
