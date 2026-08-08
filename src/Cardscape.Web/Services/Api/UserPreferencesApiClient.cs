using System.Net.Http;
using System.Net.Http.Json;

namespace Cardscape.Web.Services.Api;

/// <summary>
/// Client for the per-user appearance preferences endpoints
/// (added by docs/roadmap/06-plan-radzen-themes.md commit 2).
/// The DTO shape matches the server's
/// <c>Cardscape.Application.UserPreferences.DTOs.UserPreferencesDto</c>;
/// <c>Mode</c> is a string ("Light" / "Dark" / "System") per
/// the wire contract, so the client side does not need to
/// know about the enum values.
/// </summary>
public interface IUserPreferencesApiClient
{
    /// <summary>GET /api/users/me/preferences. Returns null
    /// for fresh users (no row yet) — the caller is
    /// expected to POST first to create the row.</summary>
    Task<ApiResult<UserPreferencesDto?>> GetAsync(CancellationToken ct = default);

    /// <summary>POST /api/users/me/preferences. Idempotent —
    /// creates the row with project defaults if missing,
    /// returns the existing one otherwise.</summary>
    Task<ApiResult<UserPreferencesDto>> CreateDefaultAsync(CancellationToken ct = default);

    /// <summary>PUT /api/users/me/preferences. Body is
    /// <c>{ themeName?, mode? }</c> — either or both fields
    /// can be supplied. Returns 404 if the user has no row
    /// yet (the caller is expected to call
    /// <see cref="CreateDefaultAsync"/> first).</summary>
    Task<ApiResult<UserPreferencesDto>> UpdateAsync(
        string? themeName, string? mode, CancellationToken ct = default);
}

public sealed class UserPreferencesApiClient(IHttpClientFactory httpClientFactory)
    : ApiClientBase(httpClientFactory), IUserPreferencesApiClient
{
    public async Task<ApiResult<UserPreferencesDto?>> GetAsync(CancellationToken ct = default)
    {
        try
        {
            using HttpClient http = CreateClient();
            using HttpResponseMessage response = await http.GetAsync(
                "api/users/me/preferences", ct);
            return await ReadAsync<UserPreferencesDto?>(response, ct);
        }
        catch (Exception ex)
        {
            return ApiResult<UserPreferencesDto?>.Fail(ex.Message);
        }
    }

    public async Task<ApiResult<UserPreferencesDto>> CreateDefaultAsync(CancellationToken ct = default)
    {
        try
        {
            using HttpClient http = CreateClient();
            using HttpResponseMessage response = await http.PostAsync(
                "api/users/me/preferences", content: null, ct);
            return await ReadAsync<UserPreferencesDto>(response, ct);
        }
        catch (Exception ex)
        {
            return ApiResult<UserPreferencesDto>.Fail(ex.Message);
        }
    }

    public async Task<ApiResult<UserPreferencesDto>> UpdateAsync(
        string? themeName, string? mode, CancellationToken ct = default)
    {
        try
        {
            using HttpClient http = CreateClient();
            using HttpResponseMessage response = await http.PutAsJsonAsync(
                "api/users/me/preferences", new { themeName, mode }, ct);
            return await ReadAsync<UserPreferencesDto>(response, ct);
        }
        catch (Exception ex)
        {
            return ApiResult<UserPreferencesDto>.Fail(ex.Message);
        }
    }
}

public sealed record UserPreferencesDto(
    Guid UserId,
    string ThemeName,
    string Mode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
