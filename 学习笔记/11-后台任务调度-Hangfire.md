# PlatformBase 后台任务调度 (Hangfire) — 手把手教学笔记

---

## 一、Hangfire 是什么？

Hangfire 是一个 .NET 后台任务调度库，支持：
- **立即执行**：`BackgroundJob.Enqueue(() => DoWork())`
- **延迟执行**：`BackgroundJob.Schedule(() => DoWork(), TimeSpan.FromMinutes(5))`
- **定时执行**：`RecurringJob.AddOrUpdate("id", () => DoWork(), "0 3 * * *")`
- **Dashboard**：Web 界面实时监控任务状态

---

## 二、架构设计

```
┌─────────────────────────────────────────────────────────────────┐
│                     Hangfire 任务调度架构                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────────────┐      ┌──────────────────────┐             │
│  │ Program.cs 启动   │      │ Hangfire Dashboard    │             │
│  │                  │      │ /hangfire             │             │
│  │ AddHangfire-     │      │ (仅 Admin 角色可访问)  │             │
│  │ Infrastructure() │      └──────────────────────┘             │
│  │ UseHangfireSync- │                                           │
│  │ Async()          │                                           │
│  └────────┬─────────┘                                           │
│           │                                                     │
│           ▼                                                     │
│  ┌──────────────────────────────────────────────────────┐       │
│  │              BackgroundJobService                      │       │
│  │                                                       │       │
│  │  ① SyncFromDatabaseAsync()  启动时从 DB 同步任务       │       │
│  │  ② Enqueue(jobId)           立即执行                   │       │
│  │  ③ StartRecurring(id, cron) 注册定时任务               │       │
│  │  ④ StopRecurring(id)        停止任务                  │       │
│  │  ⑤ UpdateCron(id, newCron)  动态修改 Cron             │       │
│  │  ⑥ TriggerNow(id)           手动触发                  │       │
│  │  ⑦ GetAllStatusesAsync()    查询状态                  │       │
│  └──────────────┬───────────────────────────────────────┘       │
│                 │                                               │
│    ┌────────────┴────────────┐                                  │
│    ▼                         ▼                                  │
│  ┌──────────────┐    ┌──────────────┐                           │
│  │ JobRegistry  │    │ JobSchedule  │                           │
│  │ 内置任务定义  │    │ 表 (DB 配置) │                           │
│  │ cleanup-     │    │ JobId        │                           │
│  │ expired-     │    │ Cron         │                           │
│  │ tokens       │    │ IsEnabled    │                           │
│  └──────────────┘    └──────────────┘                           │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 三、核心实现

### 3.1 JobRegistry — 内置任务定义

```csharp
public static class JobRegistry
{
    public static readonly IReadOnlyList<JobDefinition> AllJobs = new List<JobDefinition>
    {
        new()
        {
            JobId = "cleanup-expired-tokens",
            JobName = "过期Token清理",
            DefaultCron = "0 3 * * *",          // 每天凌晨 3 点
            Description = "清理 30 天前过期的 RefreshToken 记录",
            JobType = typeof(CleanupExpiredTokensJob)
        }
    };

    public static JobDefinition? Find(string jobId)
        => AllJobs.FirstOrDefault(j => j.JobId == jobId);
}

public class JobDefinition
{
    public string JobId { get; init; }       // 唯一标识
    public string JobName { get; init; }     // 显示名称
    public string DefaultCron { get; init; } // 默认 Cron 表达式
    public string Description { get; init; } // 说明
    public Type JobType { get; init; }       // Job 类型
}
```

### 3.2 JobSchedule — 数据库配置表

```csharp
public class JobSchedule : AuditableEntity
{
    public string JobId { get; set; }             // 对应 JobRegistry 的 JobId
    public string JobName { get; set; }           // 显示名称
    public string CronExpression { get; set; }    // Cron 表达式（6 位）
    public bool IsEnabled { get; set; } = true;   // 是否启用
    public string? Description { get; set; }      // 说明
    public DateTime? LastRunAt { get; set; }      // 上次执行时间
    public string? LastError { get; set; }        // 最近错误信息
}
```

**设计要点：** 任务的状态（启用/禁用、Cron 表达式、最后执行时间）存储在数据库中，而不是硬编码。管理员可通过 API 修改 Cron 表达式，无需重新部署。

### 3.3 BackgroundJobService — 核心服务

```csharp
public class BackgroundJobService : IBackgroundJobService
{
    // 启动时从数据库同步任务到 Hangfire
    public async Task SyncFromDatabaseAsync(CancellationToken ct)
    {
        // ① 把 JobRegistry 中的内置任务注册到数据库（如果不存在）
        foreach (var def in JobRegistry.AllJobs)
        {
            var existing = await _scheduleService.GetByJobIdAsync(def.JobId, ct);
            if (existing == null)
            {
                await _scheduleService.UpsertAsync(new JobSchedule
                {
                    JobId = def.JobId,
                    JobName = def.JobName,
                    CronExpression = def.DefaultCron,
                    IsEnabled = true,
                    Description = def.Description
                }, ct);
            }
        }

        // ② 从数据库中读取启用的任务，注册到 Hangfire
        var schedules = await _scheduleService.GetAllAsync(ct);
        foreach (var schedule in schedules.Where(s => s.IsEnabled))
        {
            var definition = JobRegistry.Find(schedule.JobId);
            if (definition == null) continue;

            _recurringJobManager.AddOrUpdate(
                schedule.JobId,
                Job.FromExpression(() => ExecuteJobAsync(definition.JobType, CancellationToken.None)),
                schedule.CronExpression);
        }
    }

    // 立即执行
    public void Enqueue(string jobId)
    {
        var definition = JobRegistry.Find(jobId)
            ?? throw new InvalidOperationException($"未找到任务: {jobId}");
        BackgroundJob.Enqueue(() => ExecuteJob(definition.JobType));
    }

    // 动态修改 Cron
    public void UpdateCron(string jobId, string newCron)
    {
        _recurringJobManager.AddOrUpdate(jobId,
            Job.FromExpression(() => ExecuteJobAsync(...)),
            newCron);
    }
}
```

### 3.4 CleanupExpiredTokensJob — 示例 Job

```csharp
public class CleanupExpiredTokensJob : IRecurringJob
{
    private readonly AppDbContext _db;

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var expired = await _db.PersistedGrants
            .Where(g => g.Expiration < thirtyDaysAgo)
            .ExecuteDeleteAsync(ct);
    }
}
```

### 3.5 OperationLogWriterJob — 日志写入 Job

```csharp
public class OperationLogWriterJob
{
    [AutomaticRetry(Attempts = 0)]   // 失败不重试
    public async Task WriteAsync(OperationLogEntry entry, CancellationToken ct)
    {
        await _logService.WriteAsync(entry, ct);
    }
}

// 调用方（OperationLogFilter）
BackgroundJob.Enqueue<OperationLogWriterJob>(job => job.WriteAsync(entry, CancellationToken.None));
```

---

## 四、Hangfire Dashboard

```csharp
// Program.cs 中配置
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAuthFilter()],  // 仅 Admin 角色可访问
    DashboardTitle = "PlatformBase 任务调度"
});
```

访问 `http://localhost:5269/hangfire` 即可查看：
- 所有 Recurring Job 的 Cron 表达式和下次执行时间
- 历史执行记录（成功/失败）
- 失败任务的手动重试

---

## 五、Cron 表达式速查

```
┌─────────── 分 (0-59)
│ ┌───────── 时 (0-23)
│ │ ┌─────── 日 (1-31)
│ │ │ ┌───── 月 (1-12)
│ │ │ │ ┌─── 星期 (0-6, 0=周日)
│ │ │ │ │
* * * * *

常用示例：
  0 3 * * *     每天凌晨 3 点
  */5 * * * *   每 5 分钟
  0 */2 * * *   每 2 小时
  0 9 * * 1-5   工作日早上 9 点
  0 0 1 * *     每月 1 号凌晨
```

---

## 六、架构全景图

```
启动流程:
  app.Run() 之前
    → await app.SeedAsync()            // 种子数据
    → await app.UseHangfireSyncAsync() // 同步任务配置
       │
       ├─ JobRegistry.AllJobs          // 内置任务定义
       │   └─ "cleanup-expired-tokens"
       │
       ├─ 写入 JobSchedules 表         // 持久化配置
       │
       └─ 启用任务注册到 Hangfire      // 实际调度

运行时:
  POST /api/jobs/{id}/trigger          → BackgroundJobService.TriggerNow
  PUT  /api/jobs/{id}/cron {cron}      → BackgroundJobService.UpdateCron
  PUT  /api/jobs/{id}/enable           → StartRecurring
  PUT  /api/jobs/{id}/disable          → StopRecurring
  GET  /api/jobs                       → GetAllStatusesAsync

  /hangfire Dashboard                  → 可视化监控
```

---

## 七、最佳实践速查卡

```
┌─────────────────────────────────────────────────────────────────┐
│            Hangfire 任务调度 黄金法则                             │
├─────────────────────────────────────────────────────────────────┤
│  1. JobRegistry 定义内置任务，JobSchedule 表存储可配置状态       │
│  2. 启动时 SyncFromDatabaseAsync 自动注册/更新任务               │
│  3. SQLite/MySql → UseMemoryStorage (开发环境，重启丢失)         │
│  4. SQL Server → UseSqlServerStorage (生产环境，持久化)          │
│  5. WorkerCount = CPU × 2（避免过多线程竞争）                    │
│  6. AutomaticRetry(0)：日志写入类任务不重试                      │
│  7. Hangfire Dashboard 仅 Admin 可访问（HangfireAuthFilter）     │
│  8. Cron 表达式存数据库，修改无需重启/重部署                     │
│  9. 所有 Job 注册为 Transient（每次执行新实例）                   │
│ 10. OperationLog 通过 Enqueue 异步入队，不阻塞 HTTP 响应          │
└─────────────────────────────────────────────────────────────────┘
```
