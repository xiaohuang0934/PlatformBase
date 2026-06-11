using PlatformBase.Core;
using StackExchange.Redis;

namespace PlatformBase.Host.Services.CacheModule;

/// <summary>
/// Redis 分布式锁实现
/// </summary>
public class RedisLockService : ILockService
{
    private readonly IDatabase? _redis;
    public RedisLockService(IServiceProvider sp) =>
        _redis = sp.GetService<IConnectionMultiplexer>()?.GetDatabase();

    public async Task<bool> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        if (_redis == null) return true; // Redis不可用→跳过锁
        return await _redis.StringSetAsync($"lock:{key}", Environment.MachineName, expiry, When.NotExists);
    }

    public async Task ReleaseAsync(string key, CancellationToken ct = default)
    {
        if (_redis == null) return;
        await _redis.KeyDeleteAsync($"lock:{key}");
    }
}
