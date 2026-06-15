using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.SqlServer;
using PlatformBase.Core;
using PlatformBase.Host.Jobs;

namespace PlatformBase.Host.Extensions;

/// <summary>
/// Hangfire 基础设施注册扩展
/// 根据数据库提供器自动适配存储引擎，注册 Dashboard 授权过滤器
/// SQL Server 使用 SqlServerStorage，其余使用 InMemoryStorage（开发环境）
/// </summary>
public static class HangfireExtensions
{
    public static IServiceCollection AddHangfireInfrastructure(
        this IServiceCollection services,
        DatabaseProvider dbProvider,
        string connectionString)
    {
        services.AddHangfire(config =>
        {
            config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180);
            config.UseSimpleAssemblyNameTypeSerializer();
            config.UseRecommendedSerializerSettings();

            if (dbProvider == DatabaseProvider.SqlServer)
            {
                config.UseSqlServerStorage(connectionString,
                    new SqlServerStorageOptions
                    {
                        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                        QueuePollInterval = TimeSpan.FromSeconds(15),
                        UseRecommendedIsolationLevel = true
                    });
            }
            else
            {
                config.UseMemoryStorage();
            }
        });

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = Environment.ProcessorCount * 2;
            options.Queues = ["default"];
        });

        // 注册所有 Job 为 Transient（每次执行创建新实例）
        foreach (var definition in JobRegistry.AllJobs)
            services.AddTransient(definition.JobType);

        // 注册操作日志写入 Job
        services.AddTransient<OperationLogWriterJob>();

        return services;
    }

    /// <summary>应用启动后同步任务配置到 Hangfire</summary>
    public static async Task UseHangfireSyncAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("HangfireSync");

        try
        {
            var bgService = scope.ServiceProvider.GetRequiredService<IBackgroundJobService>();
            await bgService.SyncFromDatabaseAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Hangfire 任务同步失败，应用将继续启动，可通过 API 手动管理任务");
        }
    }
}
