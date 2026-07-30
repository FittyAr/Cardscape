using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.Domain.Integrations.GoogleDrive;

/// <summary>
/// Per-user Google Drive connection. Holds the encrypted refresh
/// token (the access token is short-lived and re-fetched on every
/// call from the implementation) and the user's Google account
/// email. Soft-deleted connections stay in the table so the
/// audit history is preserved.
/// </summary>
public sealed class GoogleDriveConnection : AggregateRoot<GoogleDriveConnectionId>
{
    public UserId UserId { get; private set; } = null!;

    /// <summary>The user's Google account email (lowercased).</summary>
    public string GoogleEmail { get; private set; } = string.Empty;

    /// <summary>
    /// Encrypted refresh token (protected with
    /// <c>ISecretProtector</c>). The cleartext is held in memory
    /// only by the default
    /// <c>HttpGoogleDrivePickerService</c> after a successful
    /// exchange.
    /// </summary>
    public string EncryptedRefreshToken { get; private set; } = string.Empty;

    /// <summary>UTC timestamp of the last successful call, or
    /// <c>null</c> if no call has succeeded yet.</summary>
    public DateTimeOffset? LastUsedAt { get; private set; }

    public bool Active { get; private set; } = true;

    // EF Core.
    private GoogleDriveConnection() { }

    private GoogleDriveConnection(
        GoogleDriveConnectionId id,
        UserId userId,
        string googleEmail,
        string encryptedRefreshToken,
        DateTimeOffset at)
    {
        Id = id;
        UserId = userId;
        GoogleEmail = googleEmail;
        EncryptedRefreshToken = encryptedRefreshToken;
        Active = true;
        CreatedAt = at;
    }

    public static Result<GoogleDriveConnection> Connect(
        GoogleDriveConnectionId id,
        UserId userId,
        string googleEmail,
        string encryptedRefreshToken,
        DateTimeOffset at)
    {
        if (userId.Value == Guid.Empty)
        {
            return Result.Failure<GoogleDriveConnection>(DomainError.Validation(
                "google_drive.user_required", "User is required."));
        }

        if (string.IsNullOrWhiteSpace(googleEmail))
        {
            return Result.Failure<GoogleDriveConnection>(DomainError.Validation(
                "google_drive.email_required", "Google account email is required."));
        }

        if (googleEmail.Length > 320)
        {
            return Result.Failure<GoogleDriveConnection>(DomainError.Validation(
                "google_drive.email_too_long", "Google account email must be 320 characters or fewer."));
        }

        if (string.IsNullOrWhiteSpace(encryptedRefreshToken))
        {
            return Result.Failure<GoogleDriveConnection>(DomainError.Validation(
                "google_drive.refresh_token_required", "Refresh token is required."));
        }

        return Result.Success(new GoogleDriveConnection(
            id, userId, googleEmail.Trim().ToLowerInvariant(),
            encryptedRefreshToken, at));
    }

    public void RecordUse(DateTimeOffset at)
    {
        if (!Active)
        {
            return;
        }

        LastUsedAt = at;
        UpdatedAt = at;
    }

    public void Deactivate(DateTimeOffset at)
    {
        if (!Active)
        {
            return;
        }

        Active = false;
        UpdatedAt = at;
    }

    public void Activate(DateTimeOffset at)
    {
        if (Active)
        {
            return;
        }

        Active = true;
        UpdatedAt = at;
    }
}
