namespace PlatformBase.Host.Filters;

/// <summary>
/// 操作日志标记特性，标注在需要记录操作日志的 Controller Action 上
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class OperationLogAttribute : Attribute
{
    /// <summary>操作类型（login / create / update / delete / export）</summary>
    public string Action { get; }

    /// <summary>资源描述（可选，默认取 controller.action）</summary>
    public string? Resource { get; set; }

    /// <summary>是否捕获请求参数快照，默认 true</summary>
    public bool CaptureArgs { get; set; } = true;

    public OperationLogAttribute(string action)
    {
        Action = action;
    }
}
