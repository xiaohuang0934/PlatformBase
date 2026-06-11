using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using PlatformBase.Application.Dtos;
using PlatformBase.Application.Services;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Host.Authorization;

namespace PlatformBase.Host.Controllers.FileModule;

/// <summary>
/// 文件管理 API 控制器，提供上传/下载/删除/检索
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/files")]
public class FileController : ControllerBase
{
    private readonly IFileService _service;

    public FileController(IFileService service)
    {
        _service = service;
    }

    /// <summary>上传文件（multipart/form-data）</summary>
    [HttpPost("upload")]
    [Permission("files.upload")]
    public async Task<ApiResult<FileAttachmentDto>> Upload(
        IFormFile file,
        [FromQuery] string bucket = "default",
        [FromQuery] string? bizType = null,
        [FromQuery] Guid? bizId = null,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return ApiResult<FileAttachmentDto>.Fail(ErrorCode.BadRequest, "请选择文件");

        await using var stream = file.OpenReadStream();
        var result = await _service.UploadAsync(bucket, file.FileName, stream, bizType, bizId, ct);
        return ApiResult<FileAttachmentDto>.Ok(result);
    }

    /// <summary>下载文件（流式输出）</summary>
    [HttpGet("{id:guid}/download")]
    [Permission("files.upload")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        try
        {
            var (stream, mimeType, originalName) = await _service.GetAsync(id, ct);
            return File(stream, mimeType, originalName, enableRangeProcessing: true);
        }
        catch (BusinessException ex) when (ex.Code == ErrorCode.DataNotFound)
        {
            return Ok(ApiResult.Fail(ErrorCode.DataNotFound, "文件不存在"));
        }
    }

    /// <summary>删除文件（软删除）</summary>
    [HttpDelete("{id:guid}")]
    [Permission("files.upload")]
    public async Task<ApiResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return ApiResult.Ok("删除成功");
    }

    /// <summary>按业务类型和 ID 查找所有附件</summary>
    [HttpGet]
    [Permission("files.upload")]
    public async Task<ApiResult<IReadOnlyList<FileAttachmentDto>>> GetByBiz(
        [FromQuery] string bizType, [FromQuery] Guid bizId, CancellationToken ct)
    {
        var result = await _service.GetByBizAsync(bizType, bizId, ct);
        return ApiResult<IReadOnlyList<FileAttachmentDto>>.Ok(result);
    }
}
