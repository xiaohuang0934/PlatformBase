namespace PlatformBase.Host.StorageProviders;

/// <summary>
/// 文件存储提供器抽象，支持本地文件系统和 OSS 可插拔实现
/// </summary>
public interface IFileStorageProvider
{
    /// <summary>保存文件流，返回存储 Key</summary>
    Task<string> SaveAsync(Stream stream, string bucket, string key, CancellationToken ct = default);

    /// <summary>获取文件流</summary>
    Task<Stream> GetAsync(string bucket, string key, CancellationToken ct = default);

    /// <summary>删除文件</summary>
    Task DeleteAsync(string bucket, string key, CancellationToken ct = default);
}
