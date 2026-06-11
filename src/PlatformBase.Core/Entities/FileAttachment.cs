namespace PlatformBase.Core.Entities;

/// <summary>
/// 文件附件实体，记录文件存储元信息
/// 通过 IFileStorageProvider 抽象支持本地存储和 OSS 可插拔
/// 继承 <see cref="SoftDeleteEntity"/> 获得审计追踪 + 软删除能力
/// </summary>
public class FileAttachment : TenantSoftDeleteEntity
{
    /// <summary>存储桶名（local / avatar / document）</summary>
    public string Bucket { get; set; } = string.Empty;

    /// <summary>存储路径（如 2026/06/abc.png）</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>原始文件名</summary>
    public string OriginalName { get; set; } = string.Empty;

    /// <summary>文件大小（字节）</summary>
    public long Size { get; set; }

    /// <summary>MIME 类型（如 image/png）</summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>业务类型（User / Product 等）</summary>
    public string? BizType { get; set; }

    /// <summary>业务实体 ID</summary>
    public Guid? BizId { get; set; }
}
