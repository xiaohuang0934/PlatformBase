namespace PlatformBase.Core.Primitives;

/// <summary>
/// DDD强类型ID值对象基类，解决Primitive Obsession问题
/// </summary>
/// <typeparam name="T">ID值的底层类型，如Guid、long、int</typeparam>
/// <remarks>
/// 使用示例：
/// <code>
/// public record UserId : TypedId&lt;Guid&gt;
/// {
///     public UserId(Guid value) : base(value) { }
///     public static UserId New() => new(Guid.NewGuid());
/// }
/// </code>
/// EF Core配置（无需ValueConverter）：
/// <code>
/// builder.Property(x => x.Id)
///        .HasConversion(id => id.Value, value => new UserId(value));
/// </code>
/// </remarks>
public abstract record TypedId<T> : IComparable<TypedId<T>> where T : struct, IComparable<T>
{
    /// <summary>原始ID值</summary>
    public T Value { get; }

    protected TypedId(T value)
    {
        if (EqualityComparer<T>.Default.Equals(value, default))
        {
            throw new ArgumentException($"ID value cannot be default({typeof(T).Name})", nameof(value));
        }
        Value = value;
    }

    public int CompareTo(TypedId<T>? other)
    {
        if (other is null) return 1;
        return Value.CompareTo(other.Value);
    }

    public override string ToString() => Value.ToString() ?? string.Empty;
}
