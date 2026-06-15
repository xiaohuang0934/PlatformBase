using System.Globalization;
using System.Reflection;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;

namespace PlatformBase.Host.Services.ImportExportModule;

/// <summary>
/// 通用 Excel/CSV 导入导出服务实现
/// </summary>
public class ImportExportService : IExportService, IImportService
{
    public Task<Stream> ExportExcelAsync<T>(IReadOnlyList<T> data, IReadOnlyList<ColumnMapping> columns,
        string sheetName = "Sheet1", CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);

        // 表头
        for (int c = 0; c < columns.Count; c++)
            sheet.Cell(1, c + 1).Value = columns[c].Header;

        // 数据行
        for (int r = 0; r < data.Count; r++)
        {
            for (int c = 0; c < columns.Count; c++)
            {
                var prop = typeof(T).GetProperty(columns[c].Property,
                    BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                    sheet.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(prop.GetValue(data[r]) ?? "");
            }
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return Task.FromResult((Stream)stream);
    }

    public async Task<(IReadOnlyList<T> Data, IReadOnlyList<ImportRowError> Errors)> ImportExcelAsync<T>(
        Stream stream, IReadOnlyList<ColumnMapping> columns,
        Func<T, string?> validator, CancellationToken ct = default) where T : new()
    {
        var data = new List<T>();
        var errors = new List<ImportRowError>();

        try
        {
            // 尝试 Excel，失败则尝试 CSV
            await TryImportExcelAsync(stream, columns, data, errors, ct);
        }
        catch
        {
            stream.Position = 0;
            await TryImportCsvAsync(stream, columns, data, errors, ct);
        }

        // 校验
        for (int i = data.Count - 1; i >= 0; i--)
        {
            var error = validator(data[i]);
            if (error != null)
            {
                errors.Add(new ImportRowError { Row = i + 1, Error = error });
                data.RemoveAt(i);
            }
        }

        return (data, errors);
    }

    private static Task TryImportExcelAsync<T>(Stream stream, IReadOnlyList<ColumnMapping> columns,
        List<T> data, List<ImportRowError> errors, CancellationToken ct) where T : new()
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        var rows = sheet.RowsUsed().Skip(1); // 跳过表头

        foreach (var row in rows)
        {
            try
            {
                var obj = new T();
                for (int c = 0; c < columns.Count; c++)
                {
                    var prop = typeof(T).GetProperty(columns[c].Property,
                        BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                    if (prop == null) continue;

                    var cellValue = row.Cell(c + 1).Value;
                    prop.SetValue(obj, Convert.ChangeType(cellValue, prop.PropertyType));
                }
                data.Add(obj);
            }
            catch (Exception ex)
            {
                errors.Add(new ImportRowError { Row = row.RowNumber(), Error = ex.Message });
            }
        }

        return Task.CompletedTask;
    }

    private static async Task TryImportCsvAsync<T>(Stream stream, IReadOnlyList<ColumnMapping> columns,
        List<T> data, List<ImportRowError> errors, CancellationToken ct) where T : new()
    {
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));

        await csv.ReadAsync();
        csv.ReadHeader();

        var rowNum = 1;
        while (await csv.ReadAsync())
        {
            try
            {
                var obj = new T();
                for (int c = 0; c < columns.Count; c++)
                {
                    var prop = typeof(T).GetProperty(columns[c].Property,
                        BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                    if (prop == null) continue;

                    var value = csv.GetField(columns[c].Header);
                    if (value != null)
                        prop.SetValue(obj, Convert.ChangeType(value, prop.PropertyType));
                }
                data.Add(obj);
            }
            catch (Exception ex)
            {
                errors.Add(new ImportRowError { Row = rowNum, Error = ex.Message });
            }
            rowNum++;
        }
    }
}
