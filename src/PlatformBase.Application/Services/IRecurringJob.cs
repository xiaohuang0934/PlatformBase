namespace PlatformBase.Application.Services;

/// <summary>
/// 周期性任务标记接口，所有后台任务必须实现此接口
/// </summary>
public interface IRecurringJob
{
    Task ExecuteAsync(CancellationToken cancellationToken);
}
