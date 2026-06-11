namespace PlatformBase.Application.Dtos;

/// <summary>
/// 文件附件 DTO
/// </summary>
public class FileAttachmentDto
{
    public Guid Id { get; set; }
    public string Bucket { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public long Size { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public string? BizType { get; set; }
    public Guid? BizId { get; set; }
    public DateTime CreatedAt { get; set; }
}
