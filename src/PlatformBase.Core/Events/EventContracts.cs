namespace PlatformBase.Core.Events;

/// <summary>
/// 事件标记接口，所有领域事件必须实现
/// </summary>
public interface IEvent { }

/// <summary>
/// 事件发布器抽象，支持后期无缝切换为 RabbitMQ
/// </summary>
public interface IEventPublisher
{
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;
}

/// <summary>
/// 事件处理器泛型接口，每个事件类型可有多个 Handler
/// </summary>
public interface IEventHandler<in TEvent> where TEvent : class, IEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}
