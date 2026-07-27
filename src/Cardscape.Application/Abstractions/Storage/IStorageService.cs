namespace Cardscape.Application.Abstractions.Storage;

/// <summary>
/// Abstraction over the blob store used for attachments.
/// Implementations can target local disk, S3, Azure Blob, etc.
/// </summary>
public interface IStorageService
{
    /// <summary>Stores a stream under the given key and returns a public-facing URL or path.</summary>
    Task<string> SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Opens a stored stream for reading.</summary>
    Task<Stream> OpenReadAsync(string key, CancellationToken ct = default);

    /// <summary>Removes a stored object.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);
}
