namespace PlatformBase.Core;

/// <summary>
/// 分布式 ID 生成器（预留接口，可用于 Snowflake 或 Guid）
/// </summary>
public interface IIdGenerator
{
    long NewId();
}

/// <summary>Guid 实现（默认）</summary>
public class GuidIdGenerator : IIdGenerator
{
    public long NewId() => Guid.NewGuid().GetHashCode();
}
