namespace PlatformBase.Core.Entities;

/// <summary>
/// 平台账号-租户 M:N 映射表
/// </summary>
public class PlatformUserTenant
{
    /// <summary>平台账号 ID</summary>
    public Guid PlatformUserId { get; set; }

    /// <summary>租户 ID</summary>
    public Guid TenantId { get; set; }
}
