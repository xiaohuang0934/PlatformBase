namespace PlatformBase.Application.Dtos;

/// <summary>
/// DTO基类，所有数据传输对象继承自此
/// </summary>
public abstract class BaseDto
{
    /// <summary>主键ID（Guid）</summary>
    public Guid Id { get; set; }
}
