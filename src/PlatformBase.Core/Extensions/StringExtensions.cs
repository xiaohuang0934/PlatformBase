namespace PlatformBase.Core.Extensions;

/// <summary>
/// 字符串扩展工具方法
/// </summary>
public static class StringExtensions
{
    /// <summary>规范化字符串（大写，用于大小写不敏感的比较和索引）</summary>
    public static string Normalize(string? value) => (value ?? string.Empty).ToUpperInvariant();
}
