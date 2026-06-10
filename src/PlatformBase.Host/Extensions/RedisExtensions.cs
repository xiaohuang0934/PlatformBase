using StackExchange.Redis;

namespace PlatformBase.Host.Extensions;

/// <summary>
/// Redis DI 注册扩展方法
/// 组合策略：
///   A. Redis:Enabled=false → 直接跳过注册（零开销）
///   B. Redis:Enabled=true → Connect + Ping 探测 → 失败则打 Warn 跳过
/// 下游 PermissionService 通过构造注入 null 感知，自动跳过缓存
/// </summary>
public static class RedisExtensions
{
    public static IServiceCollection AddRedis(
        this IServiceCollection services, IConfiguration configuration)
    {
        // 方案 A：配置关闭
        var enabled = configuration.GetValue<bool>("Redis:Enabled");
        if (!enabled)
            return services;

        var connectionString = configuration.GetValue<string>("Redis:ConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
            return services;

        var multiplexer = ConnectionMultiplexer.Connect(new ConfigurationOptions
        {
            EndPoints = { connectionString },
            AbortOnConnectFail = false,
            ConnectTimeout = 5000,
            DefaultDatabase = configuration.GetValue<int?>("Redis:DefaultDatabase") ?? 0
        });

        // 方案 B：启动时 Ping 探测
        try
        {
            multiplexer.GetDatabase().Ping();
            services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        }
        catch (Exception)
        {
            multiplexer.Dispose();
            // Redis 不可用但不阻塞应用启动，下游自动降级到数据库
        }

        return services;
    }
}
