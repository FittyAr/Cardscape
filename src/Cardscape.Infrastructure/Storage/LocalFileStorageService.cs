using Cardscape.Application.Abstractions.Storage;

namespace Cardscape.Infrastructure.Storage;

/// <summary>
/// Local-disk implementation of <see cref="IStorageService"/>. The
/// root path is configurable via the <c>Storage:LocalRoot</c> key.
/// </summary>
public sealed class LocalFileStorageService : IStorageService
{
    private readonly string _root;

    public LocalFileStorageService(string root)
    {
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var fs = File.Create(path);
        await content.CopyToAsync(fs, ct);
        return key;
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var path = ResolvePath(key);
        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = ResolvePath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }

    private string ResolvePath(string key)
    {
        var combined = Path.Combine(_root, key.Replace('\\', '/').TrimStart('/'));
        var full = Path.GetFullPath(combined);
        if (!full.StartsWith(_root, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Storage key escapes the configured root.");
        }
        return full;
    }
}
