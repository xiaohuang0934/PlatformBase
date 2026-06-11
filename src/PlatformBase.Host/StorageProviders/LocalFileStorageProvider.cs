namespace PlatformBase.Host.StorageProviders;

/// <summary>
/// 本地文件系统存储实现
/// 文件存储在 {BasePath}/{bucket}/{key} 目录下
/// </summary>
public class LocalFileStorageProvider : IFileStorageProvider
{
    private readonly string _basePath;

    public LocalFileStorageProvider(IConfiguration configuration)
    {
        _basePath = configuration.GetValue<string>("FileStorage:LocalPath")
            ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
    }

    public async Task<string> SaveAsync(Stream stream, string bucket, string key,
        CancellationToken ct = default)
    {
        var dir = Path.Combine(_basePath, bucket, Path.GetDirectoryName(key) ?? "");
        Directory.CreateDirectory(dir);

        var fullPath = Path.Combine(_basePath, bucket, key);
        await using var fileStream = File.Create(fullPath);
        await stream.CopyToAsync(fileStream, ct);
        return key;
    }

    public Task<Stream> GetAsync(string bucket, string key, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, bucket, key);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"文件不存在: {bucket}/{key}");

        return Task.FromResult<Stream>(File.OpenRead(fullPath));
    }

    public Task DeleteAsync(string bucket, string key, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, bucket, key);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }
}
