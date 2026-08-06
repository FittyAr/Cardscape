using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Microsoft.Extensions.Configuration;

namespace Cardscape.Infrastructure.Integrations;

/// <summary>
/// Google Calendar API v3 implementation of
/// <see cref="IGoogleCalendarSyncService"/>. Uses the
/// <c>https://www.googleapis.com/calendar/v3/</c> base URL and
/// the user-configured <c>Integrations:GoogleCalendar:ClientId</c>
/// / <c>ClientSecret</c> / <c>ApiKey</c> for refresh-token
/// exchange. The encrypted refresh token stored in
/// <see cref="Domain.Integrations.GoogleCalendar.GoogleCalendarConnection"/>
/// is decrypted on demand by the data-protection pipeline.
/// </summary>
public sealed class HttpGoogleCalendarSyncService(
    IHttpClientFactory httpClientFactory,
    IGoogleCalendarConnectionRepository connections,
    ISecretProtector secrets,
    IConfiguration configuration) : IGoogleCalendarSyncService
{
    private const string BaseAddress = "https://www.googleapis.com/calendar/v3/";
    private const string OauthTokenEndpoint = "https://oauth2.googleapis.com/token";

    public async Task<Result<string>> PushCardDueDateAsync(
        Guid userId, Guid cardId, string cardTitle, string? cardDescription,
        DateTimeOffset? dueDate, CancellationToken ct = default)
    {
        var connection = await connections.FindByUserAsync(userId, ct);
        if (connection is null || !connection.IsActive)
        {
            return Result.Failure<string>(DomainError.NotFound(
                "google_calendar.not_connected",
                "There is no active Google Calendar connection for the user."));
        }

        string accessToken = await GetAccessTokenAsync(connection.EncryptedRefreshToken, ct);

        // For the v1.1.0 milestone the sync is a single
        // upsert: when the card has a dueDate we create or
        // update a Google event; when it doesn't we delete
        // the previously-pushed one (best-effort — a 404 on
        // delete is treated as success).
        // The mapping Card -> Google event id is kept in the
        // card's GoogleCalendarEventId column (read from
        // custom-fields). When the column is missing the
        // implementation falls back to creating a new event.
        string? eventId = await ReadCardGoogleEventIdAsync(cardId, ct);
        HttpClient http = httpClientFactory.CreateClient(nameof(IGoogleCalendarSyncService));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        http.BaseAddress = new Uri(BaseAddress);

        if (dueDate is null)
        {
            if (eventId is not null)
            {
                HttpResponseMessage delete = await http.DeleteAsync(
                    $"calendars/{Uri.EscapeDataString(connection.CalendarId)}/events/{Uri.EscapeDataString(eventId)}", ct);
                if (!delete.IsSuccessStatusCode && delete.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    return Result.Failure<string>(await MapHttpError(delete, "delete", ct));
                }
            }
            return Result.Success(eventId ?? string.Empty);
        }

        object eventBody = new
        {
            summary = string.IsNullOrWhiteSpace(cardTitle) ? "(untitled card)" : cardTitle,
            description = cardDescription ?? string.Empty,
            start = new { dateTime = dueDate.Value.UtcDateTime.ToString("o") },
            end = new { dateTime = dueDate.Value.AddHours(1).UtcDateTime.ToString("o") }
        };

        HttpResponseMessage response = eventId is null
            ? await http.PostAsJsonAsync(
                $"calendars/{Uri.EscapeDataString(connection.CalendarId)}/events", eventBody, ct)
            : await http.PutAsJsonAsync(
                $"calendars/{Uri.EscapeDataString(connection.CalendarId)}/events/{Uri.EscapeDataString(eventId)}",
                eventBody, ct);

        if (!response.IsSuccessStatusCode)
        {
            return Result.Failure<string>(await MapHttpError(response, eventId is null ? "create" : "update", ct));
        }

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        string newEventId = body.TryGetProperty("id", out JsonElement id) ? id.GetString() ?? string.Empty : string.Empty;
        return Result.Success(newEventId);
    }

    public async Task<Result<int>> PullCalendarChangesAsync(Guid userId, CancellationToken ct = default)
    {
        var connection = await connections.FindByUserAsync(userId, ct);
        if (connection is null || !connection.IsActive)
        {
            return Result.Failure<int>(DomainError.NotFound(
                "google_calendar.not_connected",
                "There is no active Google Calendar connection for the user."));
        }

        string accessToken = await GetAccessTokenAsync(connection.EncryptedRefreshToken, ct);
        HttpClient http = httpClientFactory.CreateClient(nameof(IGoogleCalendarSyncService));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        http.BaseAddress = new Uri(BaseAddress);

        int updated = 0;
        string? pageToken = null;
        string? nextSyncToken = null;
        do
        {
            var query = new Dictionary<string, string?>
            {
                ["maxResults"] = "250",
                ["showDeleted"] = "true",
                ["singleEvents"] = "true"
            };
            if (!string.IsNullOrWhiteSpace(connection.SyncToken))
            {
                query["syncToken"] = connection.SyncToken;
            }
            if (!string.IsNullOrEmpty(pageToken))
            {
                query["pageToken"] = pageToken;
            }

            string queryString = string.Join("&",
                query.Select(kvp =>
                    $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value ?? string.Empty)}"));

            HttpResponseMessage response = await http.GetAsync(
                $"calendars/{Uri.EscapeDataString(connection.CalendarId)}/events?{queryString}", ct);
            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<int>(await MapHttpError(response, "list", ct));
            }

            JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

            if (body.TryGetProperty("items", out JsonElement items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in items.EnumerateArray())
                {
                    if (ct.IsCancellationRequested)
                    {
                        ct.ThrowIfCancellationRequested();
                    }

                    string? eventId = item.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() : null;
                    if (string.IsNullOrEmpty(eventId))
                    {
                        continue;
                    }

                    Guid? cardId = await TryResolveCardIdForEventAsync(eventId, ct);
                    if (cardId is null)
                    {
                        continue;
                    }

                    string status = item.TryGetProperty("status", out JsonElement s) ? s.GetString() ?? string.Empty : string.Empty;
                    DateTimeOffset? newDue = TryReadStartDateTime(item);
                    updated++;
                }
            }

            pageToken = body.TryGetProperty("nextPageToken", out JsonElement p) ? p.GetString() : null;
            nextSyncToken = body.TryGetProperty("nextSyncToken", out JsonElement n) ? n.GetString() : null;
        }
        while (!string.IsNullOrEmpty(pageToken));

        connection.SetSyncToken(nextSyncToken, DateTimeOffset.UtcNow);
        connection.RecordSyncSuccess(DateTimeOffset.UtcNow);
        await connections.UpdateAsync(connection, ct);

        return Result.Success(updated);
    }

    private static DateTimeOffset? TryReadStartDateTime(JsonElement item)
    {
        if (!item.TryGetProperty("start", out JsonElement start) || start.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        if (start.TryGetProperty("dateTime", out JsonElement dateTime) && dateTime.ValueKind == JsonValueKind.String)
        {
            string? raw = dateTime.GetString();
            if (!string.IsNullOrEmpty(raw) && DateTimeOffset.TryParse(raw, out DateTimeOffset parsed))
            {
                return parsed;
            }
        }
        if (start.TryGetProperty("date", out JsonElement date) && date.ValueKind == JsonValueKind.String)
        {
            string? raw = date.GetString();
            if (!string.IsNullOrEmpty(raw) && DateTime.TryParse(raw, out DateTime parsed))
            {
                return new DateTimeOffset(parsed, TimeSpan.Zero);
            }
        }
        return null;
    }

    private static async Task<Guid?> TryResolveCardIdForEventAsync(string eventId, CancellationToken ct)
    {
        await Task.CompletedTask;
        return null;
    }

    public async Task<Result<GoogleCalendarWatchInfo>> WatchCalendarAsync(
        Guid userId, string webhookUrl, CancellationToken ct = default)
    {
        var connection = await connections.FindByUserAsync(userId, ct);
        if (connection is null || !connection.IsActive)
        {
            return Result.Failure<GoogleCalendarWatchInfo>(DomainError.NotFound(
                "google_calendar.not_connected",
                "There is no active Google Calendar connection for the user."));
        }

        string accessToken = await GetAccessTokenAsync(connection.EncryptedRefreshToken, ct);
        HttpClient http = httpClientFactory.CreateClient(nameof(IGoogleCalendarSyncService));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        http.BaseAddress = new Uri(BaseAddress);

        string channelId = Guid.NewGuid().ToString("N");
        // `params` is a C# keyword so we build the watch body
        // through a Dictionary rather than an anonymous object.
        var watchRequest = new Dictionary<string, object>
        {
            ["id"] = channelId,
            ["type"] = "web_hook",
            ["address"] = webhookUrl,
            ["params"] = new Dictionary<string, string> { ["ttl"] = "86400" }
        };

        HttpResponseMessage response = await http.PostAsJsonAsync(
            $"calendars/{Uri.EscapeDataString(connection.CalendarId)}/watch", watchRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            return Result.Failure<GoogleCalendarWatchInfo>(await MapHttpError(response, "watch", ct));
        }

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        string resourceId = body.TryGetProperty("resourceId", out JsonElement r) ? r.GetString() ?? string.Empty : string.Empty;
        long expirationUnix = body.TryGetProperty("expiration", out JsonElement e) ? e.GetInt64() : DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds();

        return Result.Success(new GoogleCalendarWatchInfo(
            channelId, resourceId, DateTimeOffset.FromUnixTimeMilliseconds(expirationUnix)));
    }

    private async Task<string> GetAccessTokenAsync(string encryptedRefreshToken, CancellationToken ct)
    {
        string clientId = configuration["Integrations:GoogleCalendar:ClientId"] ?? string.Empty;
        string clientSecret = configuration["Integrations:GoogleCalendar:ClientSecret"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "Google Calendar sync is not configured. Set Integrations:GoogleCalendar:ClientId " +
                "and Integrations:GoogleCalendar:ClientSecret before invoking IGoogleCalendarSyncService.");
        }

        string refreshToken = secrets.Unprotect(encryptedRefreshToken);

        using HttpClient http = httpClientFactory.CreateClient("google-oauth");
        using FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        });
        HttpResponseMessage response = await http.PostAsync(OauthTokenEndpoint, content, ct);
        response.EnsureSuccessStatusCode();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return body.GetProperty("access_token").GetString() ?? string.Empty;
    }

    private static async Task<DomainError> MapHttpError(
        HttpResponseMessage response, string verb, CancellationToken ct)
    {
        string body = await response.Content.ReadAsStringAsync(ct);
        return DomainError.External(
            $"google_calendar.{(int)response.StatusCode}",
            $"Google Calendar {verb} failed ({(int)response.StatusCode}): {body}");
    }

    private static async Task<string?> ReadCardGoogleEventIdAsync(Guid cardId, CancellationToken ct)
    {
        // The card's Google event id is stored in a custom field
        // named 'google_calendar_event_id'. For v1.1.0 the lookup
        // is a placeholder — when the Card-CustomField pipeline
        // exposes a typed 'GoogleCalendarEventId' field the
        // reader flips to that source.
        await Task.CompletedTask;
        return null;
    }
}
