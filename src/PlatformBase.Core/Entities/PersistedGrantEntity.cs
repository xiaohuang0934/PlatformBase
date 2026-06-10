namespace PlatformBase.Core.Entities;

/// <summary>
/// IdentityServer4 持久化授权存储实体
/// 映射到 PersistedGrants 表，由 IdentityServer4 的 IPersistedGrantStore 读写
/// </summary>
public class PersistedGrantEntity
{
    /// <summary>授权唯一键</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>授权类型：refresh_token / reference_token / authorization_code</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>用户 Subject ID（= UserId.ToString()）</summary>
    public string? SubjectId { get; set; }

    /// <summary>会话 ID</summary>
    public string? SessionId { get; set; }

    /// <summary>客户端 ID</summary>
    public string? ClientId { get; set; }

    /// <summary>描述信息</summary>
    public string? Description { get; set; }

    /// <summary>创建时间（UTC）</summary>
    public DateTime CreationTime { get; set; }

    /// <summary>过期时间（UTC）</summary>
    public DateTime? Expiration { get; set; }

    /// <summary>被消费时间（一次性令牌使用后标记）</summary>
    public DateTime? ConsumedTime { get; set; }

    /// <summary>序列化的完整授权数据（IdentityServer4 内部格式）</summary>
    public string Data { get; set; } = string.Empty;
}
