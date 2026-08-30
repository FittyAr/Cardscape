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
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await content.CopyToAsync(stream, ct);
                await stream.FlushAsync(ct);
            }

            File.Move(temporaryPath, path, overwrite: false);
            return key;
        }
        catch
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch
            {
                // Preserve the write/cancellation failure. A dot-prefixed
                // temporary file is never exposed as the requested object.
            }

            throw;
        }
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
