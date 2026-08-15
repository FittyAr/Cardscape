using System.Text.Json;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Domain.Integrations.GoogleCalendar;

/// <summary>
/// Strongly-typed id for <see cref="GoogleCalendarConnection"/>.
/// </summary>
public sealed record GoogleCalendarConnectionId(Guid Value) : GuidId<GoogleCalendarConnectionId>(Value);

/// <summary>
/// Per-user Google Calendar connection. Holds the encrypted
/// refresh token (granted by the Google OAuth flow), the
/// primary calendar id to sync into, and the last sync
/// timestamp. The OAuth scopes requested are
/// <c>https://www.googleapis.com/auth/calendar.events</c>
/// — read-write on events only, never on the calendar list
/// or free/busy.
/// </summary>
public sealed class GoogleCalendarConnection : AggregateRoot<GoogleCalendarConnectionId>
{
    public Guid UserId { get; private set; }
    public WorkspaceId WorkspaceId { get; private set; } = null!;
    public string GoogleEmail { get; private set; } = string.Empty;
    public string EncryptedRefreshToken { get; private set; } = string.Empty;
    public string CalendarId { get; private set; } = "primary";
    public string EventMappingsJson { get; private set; } = "{}";
    public DateTimeOffset? LastSyncedAt { get; private set; }
    public DateTimeOffset? LastSyncErrorAt { get; private set; }
    public string? LastSyncError { get; private set; }
    public bool IsActive { get; private set; } = true;

    private GoogleCalendarConnection() { }

    private GoogleCalendarConnection(
        GoogleCalendarConnectionId id,
        Guid userId,
        WorkspaceId workspaceId,
        string googleEmail,
        string encryptedRefreshToken,
        string calendarId,
        DateTimeOffset at)
    {
        Id = id;
        UserId = userId;
        WorkspaceId = workspaceId;
        GoogleEmail = googleEmail;
        EncryptedRefreshToken = encryptedRefreshToken;
        CalendarId = calendarId;
        CreatedAt = at;
        AddDomainEvent(new GoogleCalendarConnectionEstablished(
            id, userId, workspaceId, googleEmail, calendarId, at));
    }

    public static Result<GoogleCalendarConnection> Establish(
        GoogleCalendarConnectionId id,
        Guid userId,
        WorkspaceId workspaceId,
        string googleEmail,
        string encryptedRefreshToken,
        string calendarId,
        DateTimeOffset at)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<GoogleCalendarConnection>(DomainError.Validation(
                "google_calendar.user_required",
                "User id is required."));
        }

        if (string.IsNullOrWhiteSpace(googleEmail))
        {
            return Result.Failure<GoogleCalendarConnection>(DomainError.Validation(
                "google_calendar.email_required",
                "Google account email is required."));
        }

        if (string.IsNullOrWhiteSpace(encryptedRefreshToken))
        {
            return Result.Failure<GoogleCalendarConnection>(DomainError.Validation(
                "google_calendar.refresh_token_required",
                "Encrypted refresh token is required."));
        }

        return Result.Success(new GoogleCalendarConnection(
            id, userId, workspaceId, googleEmail, encryptedRefreshToken,
            string.IsNullOrWhiteSpace(calendarId) ? "primary" : calendarId, at));
    }

    /// <summary>Mark the connection as having completed a successful sync.</summary>
    public void RecordSyncSuccess(DateTimeOffset at)
    {
        LastSyncedAt = at;
        LastSyncErrorAt = null;
        LastSyncError = null;
        UpdatedAt = at;
    }

    /// <summary>Mark the connection as having failed a sync (rate limit,
    /// network blip, expired refresh token, etc.). The connection
    /// stays active so the user can retry without re-authing.</summary>
    public void RecordSyncError(string error, DateTimeOffset at)
    {
        LastSyncErrorAt = at;
        LastSyncError = string.IsNullOrWhiteSpace(error) ? "Unknown error" : error;
        UpdatedAt = at;
    }

    public string? FindEventId(Guid cardId)
    {
        Dictionary<Guid, string>? mappings = JsonSerializer.Deserialize<Dictionary<Guid, string>>(
            EventMappingsJson);
        return mappings?.GetValueOrDefault(cardId);
    }

    public void SetEventId(Guid cardId, string eventId, DateTimeOffset at)
    {
        Dictionary<Guid, string> mappings = JsonSerializer.Deserialize<Dictionary<Guid, string>>(
            EventMappingsJson) ?? [];
        mappings[cardId] = eventId;
        EventMappingsJson = JsonSerializer.Serialize(mappings);
        UpdatedAt = at;
    }

    public void RemoveEventId(Guid cardId, DateTimeOffset at)
    {
        Dictionary<Guid, string> mappings = JsonSerializer.Deserialize<Dictionary<Guid, string>>(
            EventMappingsJson) ?? [];
        if (mappings.Remove(cardId))
        {
            EventMappingsJson = JsonSerializer.Serialize(mappings);
            UpdatedAt = at;
        }
    }

    /// <summary>User-initiated revoke. Sets <see cref="IsActive"/> to
    /// false and clears the refresh token so a leaked DB row can't
    /// be used to push calendar events on the user's behalf.</summary>
    public void Revoke(DateTimeOffset at)
    {
        IsActive = false;
        EncryptedRefreshToken = string.Empty;
        UpdatedAt = at;
        AddDomainEvent(new GoogleCalendarConnectionRevoked(Id, UserId, at));
    }
}
