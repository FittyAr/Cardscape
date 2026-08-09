using Cardscape.Domain.Attachments;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// Storage for <see cref="Attachment"/> aggregates. Attachments
/// are per-card; the binary payload lives behind
/// <see cref="Attachment.StorageKey"/> on the configured
/// <c>IStorageService</c>, while this repository only owns the
/// metadata row.
/// </summary>
public interface IAttachmentRepository : IRepository<Attachment, AttachmentId>
{
    /// <summary>All non-deleted attachments for a card, in upload order.</summary>
    Task<IReadOnlyList<Attachment>> ListForCardAsync(Guid cardId, CancellationToken ct = default);

    /// <summary>Count of attachments for a card. Used by the card detail header.</summary>
    Task<int> CountForCardAsync(Guid cardId, CancellationToken ct = default);
}
