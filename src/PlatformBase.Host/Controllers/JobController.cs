using Microsoft.AspNetCore.Mvc;
using Hangfire;
using PlatformBase.Application.Services;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Host.Authorization;

namespace PlatformBase.Host.Controllers;

/// <summary>
/// 后台任务管理 API 控制器
/// 提供任务列表查看、启停控制、动态 Cron 修改、手动触发等端点
/// </summary>
[ApiController]
[Route("api/jobs")]
public class JobController : ControllerBase
{
    private readonly IBackgroundJobService _bgService;
    private readonly IJobScheduleService _scheduleService;

    public JobController(IBackgroundJobService bgService, IJobScheduleService scheduleService)
    {
        _bgService = bgService;
        _scheduleService = scheduleService;
    }

    /// <summary>获取所有任务及其运行状态</summary>
    [HttpGet]
    [Permission("jobs.list")]
    public async Task<ApiResult<IReadOnlyList<JobStatusModel>>> GetAll(CancellationToken ct)
    {
        var result = await _bgService.GetAllStatusesAsync(ct);
        return ApiResult<IReadOnlyList<JobStatusModel>>.Ok(result);
    }

    /// <summary>获取单个任务详情</summary>
    [HttpGet("{jobId}")]
    [Permission("jobs.list")]
    public async Task<ApiResult<JobStatusModel>> GetById(string jobId, CancellationToken ct)
    {
        var all = await _bgService.GetAllStatusesAsync(ct);
        var item = all.FirstOrDefault(j => j.JobId == jobId);
        if (item == null)
            return ApiResult<JobStatusModel>.Fail(ErrorCode.DataNotFound, $"任务 '{jobId}' 不存在");
        return ApiResult<JobStatusModel>.Ok(item);
    }

    /// <summary>启动周期性任务</summary>
    [HttpPost("{jobId}/start")]
    [Permission("jobs.manage")]
    public async Task<ApiResult> Start(string jobId, CancellationToken ct)
    {
        var schedule = await _scheduleService.GetByJobIdAsync(jobId, ct);
        if (schedule == null)
            return ApiResult.Fail(ErrorCode.DataNotFound, $"任务 '{jobId}' 不存在");

        _bgService.StartRecurring(jobId, schedule.CronExpression);
        await _scheduleService.SetEnabledAsync(jobId, true, ct);
        return ApiResult.Ok("任务已启动");
    }

    /// <summary>停止周期性任务</summary>
    [HttpPost("{jobId}/stop")]
    [Permission("jobs.manage")]
    public async Task<ApiResult> Stop(string jobId, CancellationToken ct)
    {
        var schedule = await _scheduleService.GetByJobIdAsync(jobId, ct);
        if (schedule == null)
            return ApiResult.Fail(ErrorCode.DataNotFound, $"任务 '{jobId}' 不存在");

        _bgService.StopRecurring(jobId);
        await _scheduleService.SetEnabledAsync(jobId, false, ct);
        return ApiResult.Ok("任务已停止");
    }

    /// <summary>动态修改 Cron 表达式</summary>
    [HttpPut("{jobId}/cron")]
    [Permission("jobs.manage")]
    public async Task<ApiResult> UpdateCron(string jobId, [FromBody] UpdateCronRequest request, CancellationToken ct)
    {
        var schedule = await _scheduleService.GetByJobIdAsync(jobId, ct);
        if (schedule == null)
            return ApiResult.Fail(ErrorCode.DataNotFound, $"任务 '{jobId}' 不存在");

        _bgService.UpdateCron(jobId, request.CronExpression);
        await _scheduleService.SetCronAsync(jobId, request.CronExpression, ct);
        return ApiResult.Ok("Cron 表达式已更新");
    }

    /// <summary>手动立即触发一次</summary>
    [HttpPost("{jobId}/trigger")]
    [Permission("jobs.manage")]
    public async Task<ApiResult> Trigger(string jobId, CancellationToken ct)
    {
        var schedule = await _scheduleService.GetByJobIdAsync(jobId, ct);
        if (schedule == null)
            return ApiResult.Fail(ErrorCode.DataNotFound, $"任务 '{jobId}' 不存在");

        _bgService.TriggerNow(jobId);
        return ApiResult.Ok("任务已触发");
    }

    /// <summary>立即执行一次（火力即忘，不走 Cron 调度）</summary>
    [HttpPost("{jobId}/enqueue")]
    [Permission("jobs.manage")]
    public async Task<ApiResult> Enqueue(string jobId, CancellationToken ct)
    {
        var schedule = await _scheduleService.GetByJobIdAsync(jobId, ct);
        if (schedule == null)
            return ApiResult.Fail(ErrorCode.DataNotFound, $"任务 '{jobId}' 不存在");

        _bgService.Enqueue(jobId);
        return ApiResult.Ok("任务已加入队列");
    }
}

/// <summary>
/// 更新 Cron 表达式请求体
/// </summary>
public class UpdateCronRequest
{
    public string CronExpression { get; set; } = string.Empty;
}
