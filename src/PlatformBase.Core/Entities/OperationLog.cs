namespace PlatformBase.Core.Entities;

/// <summary>
/// 操作日志实体，记录系统关键操作（登录/数据变更/权限变更等）
/// 继承 <see cref="AuditableEntity"/> 获得创建时间审计（日志只读，不需要软删除）
/// 通过 Hangfire 异步入队写入，不阻塞 HTTP 请求
/// </summary>
public class OperationLog : TenantAuditableEntity
{
    /// <summary>操作用户 ID（未登录为 null）</summary>
    public Guid? UserId { get; set; }

    /// <summary>操作用户名快照</summary>
    public string? Username { get; set; }

    /// <summary>操作类型：login / create / update / delete / export</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>操作资源：User:admin / Role:Admin / SystemParam:site_name</summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>变更详情 JSON（可选）</summary>
    public string? Detail { get; set; }

    /// <summary>客户端 IP 地址</summary>
    public string? IpAddress { get; set; }

    /// <summary>浏览器 UserAgent</summary>
    public string? UserAgent { get; set; }

    /// <summary>是否成功</summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>操作时间</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
