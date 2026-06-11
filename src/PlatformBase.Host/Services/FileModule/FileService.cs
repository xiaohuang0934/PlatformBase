using PlatformBase.Application.Dtos;
using PlatformBase.Application.Services;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Repositories;
using PlatformBase.Host.StorageProviders;

namespace PlatformBase.Host.Services.FileModule;

/// <summary>
/// 文件管理服务实现，封装上传/下载/删除/检索
/// </summary>
public class FileService : IFileService
{
    private readonly IUnitOfWork _uow;
    private readonly IFileStorageProvider _storage;

    public FileService(IUnitOfWork uow, IFileStorageProvider storage)
    {
        _uow = uow;
        _storage = storage;
    }

    public async Task<FileAttachmentDto> UploadAsync(string bucket, string originalName, Stream stream,
        string? bizType = null, Guid? bizId = null, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(originalName);
        var key = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid()}{ext}";

        var entity = new FileAttachment
        {
            Bucket = bucket,
            Key = key,
            OriginalName = originalName,
            Size = stream.Length,
            MimeType = GetMimeType(ext),
            BizType = bizType,
            BizId = bizId
        };

        await _storage.SaveAsync(stream, bucket, key, ct);

        var created = await _uow.Repository<FileAttachment>().AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(created);
    }

    public async Task<(Stream Stream, string MimeType, string OriginalName)> GetAsync(
        Guid id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<FileAttachment>().GetByIdAsync(id, ct);
        if (entity == null)
            throw new BusinessException("文件不存在", ErrorCode.DataNotFound);

        var stream = await _storage.GetAsync(entity.Bucket, entity.Key, ct);
        return (stream, entity.MimeType, entity.OriginalName);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<FileAttachment>().GetByIdAsync(id, ct);
        if (entity == null) return;

        await _storage.DeleteAsync(entity.Bucket, entity.Key, ct);
        _uow.Repository<FileAttachment>().SoftDelete(entity);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FileAttachmentDto>> GetByBizAsync(
        string bizType, Guid bizId, CancellationToken ct = default)
    {
        var items = await _uow.Repository<FileAttachment>()
            .FindAsync(f => f.BizType == bizType && f.BizId == bizId, ct);
        return items.OrderByDescending(f => f.CreatedAt).Select(ToDto).ToList();
    }

    private static string GetMimeType(string ext) => ext.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".txt" => "text/plain",
        _ => "application/octet-stream"
    };

    private static FileAttachmentDto ToDto(FileAttachment entity) => new()
    {
        Id = entity.Id,
        Bucket = entity.Bucket,
        Key = entity.Key,
        OriginalName = entity.OriginalName,
        Size = entity.Size,
        MimeType = entity.MimeType,
        BizType = entity.BizType,
        BizId = entity.BizId,
        CreatedAt = entity.CreatedAt
    };
}
