using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cardscape.Application.Abstractions;
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
    IClock clock,
    IConfiguration configuration) : IGoogleCalendarSyncService
{
    private const string OauthTokenEndpoint = "https://oauth2.googleapis.com/token";
    private const int MaxGoogleResponseBytes = 1024 * 1024;
    private const int MaxGoogleErrorBytes = 4096;

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
        string? eventId = connection.FindEventId(cardId);
        HttpClient http = httpClientFactory.CreateClient(nameof(IGoogleCalendarSyncService));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

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
                connection.RemoveEventId(cardId, clock.UtcNow);
                await connections.UpdateAsync(connection, ct);
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
        if (string.IsNullOrWhiteSpace(newEventId))
        {
            return Result.Failure<string>(DomainError.External(
                "google_calendar.event_id_missing", "Google Calendar returned no event id."));
        }

        connection.SetEventId(cardId, newEventId, clock.UtcNow);
        await connections.UpdateAsync(connection, ct);
        return Result.Success(newEventId);
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
        using var request = new HttpRequestMessage(HttpMethod.Post, OauthTokenEndpoint) { Content = content };
        using HttpResponseMessage response = await http.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await response.Content.LoadIntoBufferAsync(MaxGoogleResponseBytes, ct);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return body.GetProperty("access_token").GetString() ?? string.Empty;
    }

    private static async Task<DomainError> MapHttpError(
        HttpResponseMessage response, string verb, CancellationToken ct)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
        byte[] buffer = new byte[MaxGoogleErrorBytes];
        int count = await stream.ReadAsync(buffer, ct);
        string body = System.Text.Encoding.UTF8.GetString(buffer, 0, count);
        return DomainError.External(
            $"google_calendar.{(int)response.StatusCode}",
            $"Google Calendar {verb} failed ({(int)response.StatusCode}): {body}");
    }

}
