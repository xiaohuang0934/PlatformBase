namespace PlatformBase.Core.Models;

/// <summary>
/// 分页查询请求参数
/// </summary>
public class PagedRequest
{
    /// <summary>页码，从1开始，默认1</summary>
    public int PageIndex { get; set; } = 1;

    /// <summary>每页条数，默认20</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>排序字段名</summary>
    public string? SortField { get; set; }

    /// <summary>是否升序，默认true</summary>
    public bool IsAscending { get; set; } = true;

    /// <summary>关键词搜索</summary>
    public string? Keyword { get; set; }

    /// <summary>从查询对象构造分页请求（链式写法用）</summary>
    public static PagedRequest From(PagedRequest query, string? sortField = null) => new()
    {
        PageIndex = query.PageIndex,
        PageSize = query.PageSize,
        SortField = sortField ?? query.SortField,
        IsAscending = query.IsAscending
    };
}

/// <summary>
/// 分页查询响应结果
/// </summary>
/// <typeparam name="T">数据项类型</typeparam>
public class PagedResult<T>
{
    /// <summary>总记录数</summary>
    public int TotalCount { get; set; }

    /// <summary>当前页码</summary>
    public int PageIndex { get; set; }

    /// <summary>每页条数</summary>
    public int PageSize { get; set; }

    /// <summary>总页数</summary>
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>当前页数据列表（只读）</summary>
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    public PagedResult() { }

    /// <summary>构造分页结果</summary>
    public PagedResult(int totalCount, int pageIndex, int pageSize, IEnumerable<T> items)
    {
        TotalCount = totalCount;
        PageIndex = pageIndex;
        PageSize = pageSize;
        Items = items.ToList().AsReadOnly();
    }
}
