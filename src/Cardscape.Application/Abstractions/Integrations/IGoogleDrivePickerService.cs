using Cardscape.Domain.Attachments;
using Cardscape.Domain.Common;

namespace Cardscape.Application.Abstractions.Integrations;

/// <summary>
/// Front door for the Google Drive integration. The default
/// implementation is an HTTP client that talks to the
/// <c>drive.google.com</c> picker flow. Two operations are
/// supported: build a picker URL the SPA can open, and resolve
/// a confirmed picker selection into a freshly-attached file on
/// a card.
/// </summary>
public interface IGoogleDrivePickerService
{
    /// <summary>
    /// Returns a Google Drive picker URL the SPA can open in a
    /// new tab. The user picks a file, returns to the SPA, and
    /// the SPA POSTs the picker response token to
    /// <c>/api/integrations/google/callback</c>.
    /// </summary>
    Task<Result<string>> BuildPickerUrlAsync(
        Guid workspaceId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Resolves a picker token into the underlying file, fetches
    /// its content, persists it via the application
    /// <c>IStorageService</c>, and creates a new
    /// <see cref="Attachment"/> on the target card. Returns the
    /// id of the new attachment.
    /// </summary>
    Task<Result<AttachmentId>> AttachFileAsync(
        Guid cardId, string fileId, string? fileName, Guid userId, CancellationToken ct = default);
}
