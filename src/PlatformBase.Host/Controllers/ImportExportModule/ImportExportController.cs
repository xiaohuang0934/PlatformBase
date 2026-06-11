using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using PlatformBase.Application.Dtos;
using PlatformBase.Application.Services;
using PlatformBase.Core.Models;
using PlatformBase.Host.Authorization;

namespace PlatformBase.Host.Controllers.ImportExportModule;

/// <summary>
/// 数据导入导出 API 控制器
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/import-export")]
public class ImportExportController : ControllerBase
{
    private readonly IExportService _exportService;
    private readonly IImportService _importService;

    public ImportExportController(IExportService exportService, IImportService importService)
    {
        _exportService = exportService;
        _importService = importService;
    }

    /// <summary>导出用户列表为 Excel</summary>
    [HttpGet("users")]
    [Permission("users.list")]
    public async Task<IActionResult> ExportUsers(
        [FromServices] IUserService userService,
        [FromQuery] string? keyword = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await userService.GetPagedAsync(new UserQuery
        {
            Keyword = keyword,
            IsActive = isActive,
            PageIndex = 1,
            PageSize = 10000 // 导出上限
        }, ct);

        var columns = new List<ColumnMapping>
        {
            new() { Header = "用户名", Property = nameof(UserDto.Username) },
            new() { Header = "邮箱", Property = nameof(UserDto.Email) },
            new() { Header = "手机号", Property = nameof(UserDto.PhoneNumber) },
            new() { Header = "启用", Property = nameof(UserDto.IsActive) },
            new() { Header = "角色", Property = nameof(UserDto.Roles) },
            new() { Header = "创建时间", Property = nameof(UserDto.CreatedAt) }
        };

        var stream = await _exportService.ExportExcelAsync(result.Items.ToList(), columns, "用户列表", ct);
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "users.xlsx");
    }

    /// <summary>导入用户</summary>
    [HttpPost("users")]
    [Permission("users.create")]
    public async Task<ApiResult<object>> ImportUsers(
        [FromServices] IUserService userService,
        IFormFile file,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return ApiResult<object>.Fail(Core.Exceptions.ErrorCode.BadRequest, "请选择文件");

        var columns = new List<ColumnMapping>
        {
            new() { Header = "用户名", Property = "Username" },
            new() { Header = "密码", Property = "Password" },
            new() { Header = "邮箱", Property = "Email" },
            new() { Header = "手机号", Property = "PhoneNumber" }
        };

        await using var stream = file.OpenReadStream();
        var (data, errors) = await _importService.ImportExcelAsync<ImportUserDto>(
            stream, columns, dto =>
            {
                if (string.IsNullOrWhiteSpace(dto.Username)) return "用户名为空";
                if (string.IsNullOrWhiteSpace(dto.Password)) return "密码为空";
                return null;
            }, ct);

        var successCount = 0;
        var errorList = errors.ToList();
        foreach (var dto in data)
        {
            try
            {
                await userService.CreateAsync(new Core.Entities.User
                {
                    Username = dto.Username,
                    NormalizedUsername = dto.Username.ToUpperInvariant(),
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                    Email = dto.Email,
                    NormalizedEmail = dto.Email?.ToUpperInvariant(),
                    PhoneNumber = dto.PhoneNumber,
                    IsActive = true
                }, ct);
                successCount++;
            }
            catch (Exception ex)
            {
                errorList.Add(new ImportRowError { Row = successCount + 1, Error = ex.Message });
            }
        }

        return ApiResult<object>.Ok(new { successCount, totalRows = successCount + errorList.Count, errors = errorList });
    }
}

/// <summary>
/// 导入用户 DTO（Internal，不对外暴露）
/// </summary>
internal class ImportUserDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
}
