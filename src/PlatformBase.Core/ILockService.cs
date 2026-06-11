namespace PlatformBase.Core;

/// <summary>
/// 分布式锁接口
/// </summary>
public interface ILockService
{
    /// <summary>尝试获取锁，成功返回 true</summary>
    Task<bool> TryAcquireAsync(string key, TimeSpan expiryTime, CancellationToken ct = default);

    /// <summary>释放锁</summary>
    Task ReleaseAsync(string key, CancellationToken ct = default);
}
