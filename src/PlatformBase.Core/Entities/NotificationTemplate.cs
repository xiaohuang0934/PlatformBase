namespace PlatformBase.Core.Entities;

/// <summary>
/// 消息通知模板实体，定义通知的标题/内容模板和发送通道
/// </summary>
public class NotificationTemplate : TenantAuditableEntity
{
    /// <summary>模板编码（唯一）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>模板名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>标题模板（支持 {变量} 替换）</summary>
    public string TitleTemplate { get; set; } = string.Empty;

    /// <summary>内容模板（支持 {变量} 替换）</summary>
    public string BodyTemplate { get; set; } = string.Empty;

    /// <summary>发送通道：in_app / email / sms</summary>
    public string Channel { get; set; } = "in_app";

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>变量说明（如：username,time）</summary>
    public string? Variables { get; set; }
}
