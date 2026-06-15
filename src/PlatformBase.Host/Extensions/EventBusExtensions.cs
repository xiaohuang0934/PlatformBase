using System.Reflection;
using PlatformBase.Core.Events;

namespace PlatformBase.Host.Extensions;

/// <summary>
/// 事件总线 DI 注册扩展
/// 自动扫描并注册所有 IEventHandler\<T\> 实现，同时注册 ChannelEventBus 为 IEventPublisher 单例
/// 后期切换 RabbitMQ 时只需更换 IEventPublisher 的注册实现即可
/// </summary>
public static class EventBusExtensions
{
    public static IServiceCollection AddEventBus(this IServiceCollection services, Assembly handlerAssembly)
    {
        // ① 注册 Channel 事件总线为 IEventPublisher 单例
        services.AddSingleton<IEventPublisher>(sp =>
        {
            var bus = new ChannelEventBus(sp, sp.GetRequiredService<ILogger<ChannelEventBus>>());
            AutoRegisterHandlers(bus, handlerAssembly);
            return bus;
        });

        // ② 自动发现并注册所有 IEventHandler<T> 为 Transient
        var handlerTypes = handlerAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                .Select(i => (HandlerType: t, EventType: i.GetGenericArguments()[0])));

        foreach (var (handler, eventType) in handlerTypes)
            services.AddTransient(handler);

        return services;
    }

    /// <summary>
    /// 自动将程序集中所有 IEventHandler&lt;T&gt; 实现注册到 ChannelEventBus
    /// </summary>
    private static void AutoRegisterHandlers(ChannelEventBus bus, Assembly handlerAssembly)
    {
        var handlerPairs = handlerAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                .Select(i => (HandlerType: t, EventType: i.GetGenericArguments()[0])));

        foreach (var (handlerType, eventType) in handlerPairs)
        {
            var registerMethod = typeof(ChannelEventBus)
                .GetMethod(nameof(ChannelEventBus.Register))!
                .MakeGenericMethod(eventType, handlerType);

            registerMethod.Invoke(bus, null);
        }
    }
}
