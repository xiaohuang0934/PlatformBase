using System.Threading.Channels;                    // 提供 Channel<T>（生产者-消费者无界队列）
using Microsoft.Extensions.DependencyInjection;         // 提供 IServiceProvider / CreateScope()
using Microsoft.Extensions.Logging;                    // 提供 ILogger<T> 结构化日志接口
using PlatformBase.Core.Events;                        // 提供 IEvent / IEventHandler<T> / IEventPublisher 接口

namespace PlatformBase.Host.Services.EventModule;

/// <summary>
/// 基于 System.Threading.Channels 的内存事件总线实现
/// 实现 IEventPublisher + IDisposable，后期可替换为 RabbitMQ 实现
///
/// 实现逻辑：
///   1. 构造函数中创建一个无界 Channel 作为事件队列（不阻塞发布者）
///   2. 启动一个后台消费者 Task（ConsumeLoopAsync），循环读取 Channel 中的事件
///   3. PublishAsync 将事件写入 Channel.Writer，立即返回，不等待处理完成
///   4. ConsumeLoopAsync 逐一取出事件，从 _handlerRegistry 查找已注册的处理器类型
///   5. 对每个处理器类型：创建 DI Scope → 解析处理器实例 → 反射调用 HandleAsync 方法
///   6. 单个处理器异常不影响其他处理器（try/catch + 日志记录）
///   7. Dispose 时取消消费者、标记 Channel 写入完成、等待最多 5 秒消费完剩余事件
/// </summary>
public sealed class ChannelEventBus : IEventPublisher, IDisposable
{
    /// <summary>无界 Channel（BoundedChannel），作为事件的中转队列，生产者和消费者解耦</summary>
    private readonly Channel<object> _channel;

    /// <summary>DI 根容器，用于在消费事件时为每个处理器创建独立的作用域</summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>事件类型 → 处理器类型列表的注册表映射（Register 方法填充）</summary>
    private readonly Dictionary<Type, List<Type>> _handlerRegistry;

    /// <summary>结构化日志记录器，用于记录事件处理的生命周期和异常</summary>
    private readonly ILogger<ChannelEventBus> _logger;

    /// <summary>取消令牌源，Dispose 时触发，通知消费者循环退出</summary>
    private readonly CancellationTokenSource _cts;

    /// <summary>后台消费者 Task，从 Channel 中读取事件并分发到各处理器</summary>
    private readonly Task _consumerTask;

    /// <summary>
    /// 构造函数：创建 Channel 并启动后台消费循环
    /// SingleWriter=false → 允许多个发布者并发写入
    /// SingleReader=true  → 消费者是唯一的，减少锁竞争
    /// </summary>
    public ChannelEventBus(IServiceProvider serviceProvider, ILogger<ChannelEventBus> logger)
    {
        _channel = Channel.CreateUnbounded<object>(new UnboundedChannelOptions // 创建无界 Channel（容量无限）
        {
            SingleWriter = false, // 多个生产者（多个 Controller 可同时发布事件）
            SingleReader = true   // 单个消费者（仅 ConsumeLoopAsync 读取，减少锁开销）
        });
        _serviceProvider = serviceProvider; // 保存 DI 根容器
        _handlerRegistry = [];              // 初始化空的处理器注册表
        _logger = logger;                  // 保存日志记录器
        _cts = new CancellationTokenSource(); // 创建取消令牌源（控制消费循环生命周期）
        _consumerTask = Task.Run(() => ConsumeLoopAsync(_cts.Token)); // 在独立 Task 中启动消费循环
    }

    /// <summary>
    /// 注册事件类型及其处理器类型映射
    /// 通常在启动时由 AddEventBus 扩展方法批量扫描并调用
    /// </summary>
    public void Register<TEvent, THandler>()
        where TEvent : class, IEvent                         // 事件类型必须实现 IEvent 接口且为引用类型
        where THandler : IEventHandler<TEvent>               // 处理器必须实现 IEventHandler<TEvent>
    {
        if (!_handlerRegistry.ContainsKey(typeof(TEvent)))   // 若该事件类型尚未在注册表中
            _handlerRegistry[typeof(TEvent)] = [];            // 为该事件类型创建空的处理器列表

        _handlerRegistry[typeof(TEvent)].Add(typeof(THandler)); // 将处理器类型添加到该事件的处理器列表中
    }

    /// <inheritdoc />
    /// <summary>
    /// 发布事件：将事件写入 Channel，立即返回（不等待处理完成）
    /// 若该事件类型无任何已注册的处理器，则跳过写入
    /// </summary>
    public async ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        if (!_handlerRegistry.ContainsKey(typeof(TEvent))) // 检查是否有注册的处理器
        {
            _logger.LogDebug("事件 {EventType} 无注册的处理器，跳过", typeof(TEvent).Name); // 无处理器时记录调试日志并返回
            return;
        }

        await _channel.Writer.WriteAsync(@event, cancellationToken); // 将事件写入 Channel 队列，等待消费者处理
    }

    /// <summary>
    /// 消费循环：从 Channel 中持续读取事件并分发到已注册的处理器
    /// 当 Channel 被标记完成且剩余事件消费完毕后自动退出
    /// </summary>
    private async Task ConsumeLoopAsync(CancellationToken ct)
    {
        await foreach (var @event in _channel.Reader.ReadAllAsync(ct)) // 异步枚举 Channel 中的所有事件（阻塞等待新事件）
        {
            var eventType = @event.GetType(); // 获取事件对象的实际类型
            if (!_handlerRegistry.TryGetValue(eventType, out var handlerTypes)) // 查找该事件类型对应的处理器列表
                continue; // 未找到处理器则跳过（理论上不会发生，Publish 已检查）

            foreach (var handlerType in handlerTypes) // 遍历该事件类型的所有已注册处理器
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope(); // 为每个处理器创建独立的 DI 作用域（隔离 DbContext 等 Scoped 服务）
                    var handler = scope.ServiceProvider.GetRequiredService(handlerType); // 从作用域中解析处理器实例
                    var handleMethod = handlerType.GetMethod(nameof(IEventHandler<IEvent>.HandleAsync)) // 通过反射获取 HandleAsync 方法
                        ?? throw new InvalidOperationException($"Handler {handlerType.Name} has no HandleAsync method"); // 方法不存在时抛出异常
                    await (Task)handleMethod.Invoke(handler, [@event, ct])!; // 反射调用 handler.HandleAsync(@event, ct)，awaits Task
                }
                catch (Exception ex) // 捕获单个处理器的异常，不影响其他处理器的执行
                {
                    _logger.LogError(ex, "事件处理失败: {EventType} → {HandlerType}", // 记录错误日志，含事件类型和处理器类型
                        eventType.Name, handlerType.Name);
                }
            }
        }
    }

    /// <summary>
    /// 释放资源：通知消费者退出 → 标记 Channel 写入完成 → 等待消费完剩余事件 → 释放 CTS
    /// 最多等待 5 秒，超时后强制退出（放弃未处理完的事件）
    /// </summary>
    public void Dispose()
    {
        _cts.Cancel();                       // 发出取消信号，通知 ConsumeLoopAsync 退出 ReadAllAsync 循环
        _channel.Writer.TryComplete();       // 标记 Channel 写入端已完成，ReadAllAsync 消费完剩余事件后也会退出
        try { _consumerTask.Wait(TimeSpan.FromSeconds(5)); } // 等待消费 Task 结束，最多阻塞 5 秒
        catch { /* 事件处理失败，不阻塞其他 Handler */ }    // 超时或异常时吞掉，避免阻塞进程退出
        _cts.Dispose();                      // 释放 CancellationTokenSource 关联的非托管资源
    }
}
