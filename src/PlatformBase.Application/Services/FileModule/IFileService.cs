using PlatformBase.Application.Dtos;
using PlatformBase.Core.Models;

namespace PlatformBase.Application.Services.FileModule;

/// <summary>
/// 文件管理服务接口，提供上传/下载/删除/检索
/// </summary>
public interface IFileService
{
    /// <summary>上传文件，返回 DTO</summary>
    Task<FileAttachmentDto> UploadAsync(string bucket, string originalName, Stream stream,
        string? bizType = null, Guid? bizId = null, CancellationToken ct = default);

    /// <summary>获取文件流（用于下载）</summary>
    Task<(Stream Stream, string MimeType, string OriginalName)> GetAsync(Guid id,
        CancellationToken ct = default);

    /// <summary>删除文件</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>按业务类型和 ID 查找所有附件</summary>
    Task<IReadOnlyList<FileAttachmentDto>> GetByBizAsync(string bizType, Guid bizId,
        CancellationToken ct = default);
}
