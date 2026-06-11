using System.Linq.Expressions;

namespace PlatformBase.Core.Extensions;

/// <summary>
/// 表达式树操作扩展
/// </summary>
public static class ExpressionExtensions
{
    /// <summary>将两个 Lambda 表达式组合为 AND 逻辑</summary>
    public static Expression<Func<T, bool>> AndAlso<T>(
        this Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var param = Expression.Parameter(typeof(T));
        var body = Expression.AndAlso(
            Expression.Invoke(left, param),
            Expression.Invoke(right, param));
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    /// <summary>
    /// 链式追加表达式（无条件）
    /// 示例: filter = filter.Append(u => u.IsActive == true);
    /// </summary>
    public static Expression<Func<T, bool>>? Append<T>(
        this Expression<Func<T, bool>>? filter,
        Expression<Func<T, bool>> predicate)
        => filter == null ? predicate : filter.AndAlso(predicate);

    /// <summary>
    /// 条件追加表达式（WhereIf 的 Expression 版）
    /// 示例: filter = filter.AppendIf(hasKeyword, u => u.Name.Contains(kw));
    /// </summary>
    public static Expression<Func<T, bool>>? AppendIf<T>(
        this Expression<Func<T, bool>>? filter,
        bool condition,
        Expression<Func<T, bool>> predicate)
        => condition ? filter.Append(predicate) : filter;
}
