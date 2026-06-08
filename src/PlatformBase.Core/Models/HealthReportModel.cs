namespace PlatformBase.Core.Models;

/// <summary>
/// 健康检查报告
/// </summary>
public class HealthReportModel
{
    /// <summary>整体健康状态：Healthy / Degraded / Unhealthy</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>检查耗时</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>各项检查详情</summary>
    public IReadOnlyList<HealthReportEntryModel> Entries { get; set; } = Array.Empty<HealthReportEntryModel>();
}

/// <summary>
/// 单项健康检查结果
/// </summary>
public class HealthReportEntryModel
{
    /// <summary>检查项名称</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>该项状态</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>该项描述</summary>
    public string? Description { get; set; }
}
