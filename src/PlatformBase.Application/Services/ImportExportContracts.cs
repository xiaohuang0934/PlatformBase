namespace PlatformBase.Application.Services;

/// <summary>
/// 通用数据导出接口，支持 Excel 导出
/// </summary>
public interface IExportService
{
    /// <summary>导出为 Excel 文件流（ClosedXML）</summary>
    Task<Stream> ExportExcelAsync<T>(IReadOnlyList<T> data, IReadOnlyList<ColumnMapping> columns,
        string sheetName = "Sheet1", CancellationToken ct = default);
}

/// <summary>
/// 通用数据导入接口，支持 Excel/CSV 解析
/// </summary>
public interface IImportService
{
    /// <summary>从 Excel 流中解析数据</summary>
    Task<(IReadOnlyList<T> Data, IReadOnlyList<ImportRowError> Errors)> ImportExcelAsync<T>(
        Stream stream, IReadOnlyList<ColumnMapping> columns,
        Func<T, string?> validator, CancellationToken ct = default) where T : new();
}

/// <summary>
/// Excel 列映射
/// </summary>
public class ColumnMapping
{
    /// <summary>列头名称</summary>
    public string Header { get; set; } = string.Empty;

    /// <summary>对象属性名</summary>
    public string Property { get; set; } = string.Empty;
}

/// <summary>
/// 导入行错误
/// </summary>
public class ImportRowError
{
    /// <summary>行号（1-based，不含表头）</summary>
    public int Row { get; set; }

    /// <summary>错误信息</summary>
    public string Error { get; set; } = string.Empty;
}
