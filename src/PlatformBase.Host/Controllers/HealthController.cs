using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PlatformBase.Core.Models;

namespace PlatformBase.Host.Controllers;

/// <summary>
/// 健康检查端点，供K8s就绪/存活探针调用
/// </summary>
[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    public HealthController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    /// <summary>
    /// 执行所有已注册的健康检查并返回汇总报告
    /// </summary>
    [HttpGet]
    public async Task<ApiResult<HealthReportModel>> Get()
    {
        var report = await _healthCheckService.CheckHealthAsync();

        var model = new HealthReportModel
        {
            Status = report.Status.ToString(),
            Duration = report.TotalDuration,
            Entries = report.Entries.Select(e => new HealthReportEntryModel
            {
                Key = e.Key,
                Status = e.Value.Status.ToString(),
                Description = e.Value.Description
            }).ToList().AsReadOnly()
        };

        return ApiResult<HealthReportModel>.Ok(model);
    }
}
