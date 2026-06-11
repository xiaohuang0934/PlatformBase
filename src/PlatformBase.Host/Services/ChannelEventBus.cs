using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlatformBase.Core.Events;

namespace PlatformBase.Host.Services;

/// <summary>
/// 基于 System.Threading.Channels 的内存事件总线实现
/// 实现 IEventPublisher + IDisposable，后期可替换为 RabbitMQ 实现
/// </summary>
public sealed class ChannelEventBus : IEventPublisher, IDisposable
{
    private readonly Channel<object> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<Type, List<Type>> _handlerRegistry;
    private readonly ILogger<ChannelEventBus> _logger;
    private readonly CancellationTokenSource _cts;
    private readonly Task _consumerTask;

    public ChannelEventBus(IServiceProvider serviceProvider, ILogger<ChannelEventBus> logger)
    {
        _channel = Channel.CreateUnbounded<object>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = true
        });
        _serviceProvider = serviceProvider;
        _handlerRegistry = [];
        _logger = logger;
        _cts = new CancellationTokenSource();
        _consumerTask = Task.Run(() => ConsumeLoopAsync(_cts.Token));
    }

    /// <summary>
    /// 注册事件类型及其处理器类型映射
    /// </summary>
    public void Register<TEvent, THandler>()
        where TEvent : class, IEvent
        where THandler : IEventHandler<TEvent>
    {
        if (!_handlerRegistry.ContainsKey(typeof(TEvent)))
            _handlerRegistry[typeof(TEvent)] = [];

        _handlerRegistry[typeof(TEvent)].Add(typeof(THandler));
    }

    /// <inheritdoc />
    public async ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        if (!_handlerRegistry.ContainsKey(typeof(TEvent)))
        {
            _logger.LogDebug("事件 {EventType} 无注册的处理器，跳过", typeof(TEvent).Name);
            return;
        }

        await _channel.Writer.WriteAsync(@event, cancellationToken);
    }

    private async Task ConsumeLoopAsync(CancellationToken ct)
    {
        await foreach (var @event in _channel.Reader.ReadAllAsync(ct))
        {
            var eventType = @event.GetType();
            if (!_handlerRegistry.TryGetValue(eventType, out var handlerTypes))
                continue;

            foreach (var handlerType in handlerTypes)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService(handlerType);
                    var handleMethod = handlerType.GetMethod(nameof(IEventHandler<IEvent>.HandleAsync))
                        ?? throw new InvalidOperationException($"Handler {handlerType.Name} has no HandleAsync method");
                    await (Task)handleMethod.Invoke(handler, [@event, ct])!;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "事件处理失败: {EventType} → {HandlerType}",
                        eventType.Name, handlerType.Name);
                }
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _channel.Writer.TryComplete();
        try { _consumerTask.Wait(TimeSpan.FromSeconds(5)); }
        catch { /* 事件处理失败，不阻塞其他 Handler */ }
        _cts.Dispose();
    }
}
