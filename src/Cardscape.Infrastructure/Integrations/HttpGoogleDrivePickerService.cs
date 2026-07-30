using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Storage;
using Cardscape.Domain.Attachments;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.GoogleDrive;
using Cardscape.Domain.Members;
using Microsoft.Extensions.Configuration;

namespace Cardscape.Infrastructure.Integrations;

/// <summary>
/// Default <see cref="IGoogleDrivePickerService"/> that talks to
/// the Google Drive REST API. The picker URL is built from the
/// configured Google OAuth client id; the file-download path
/// exchanges the stored refresh token for a fresh access token
/// and then streams the file body into the application
/// <c>IStorageService</c>.
///
/// <para>The implementation deliberately degrades gracefully when
/// configuration is missing: the build still succeeds so the
/// tests can run; every call returns a domain
/// <see cref="ErrorType.External"/> error explaining which key
/// is absent.</para>
/// </summary>
public sealed class HttpGoogleDrivePickerService : IGoogleDrivePickerService
{
    private const string GoogleOauthTokenUrl = "https://oauth2.googleapis.com/token";
    private const string GoogleDriveDownloadBase = "https://www.googleapis.com/drive/v3/files/";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly IGoogleDriveConnectionRepository _connections;
    private readonly ISecretProtector _secretProtector;
    private readonly IStorageService _storage;
    private readonly ICardRepository _cards;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public HttpGoogleDrivePickerService(
        HttpClient http,
        IConfiguration configuration,
        IGoogleDriveConnectionRepository connections,
        ISecretProtector secretProtector,
        IStorageService storage,
        ICardRepository cards,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _http = http;
        _configuration = configuration;
        _connections = connections;
        _secretProtector = secretProtector;
        _storage = storage;
        _cards = cards;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task<Result<string>> BuildPickerUrlAsync(
        Guid workspaceId, Guid userId, CancellationToken ct = default)
    {
        string? clientId = _configuration["Integrations:Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Task.FromResult(Result.Failure<string>(DomainError.External(
                "google_drive.client_id_missing",
                "Google client id is not configured (Integrations:Google:ClientId).")));
        }

        // The picker URL pattern documented at
        // https://developers.google.com/drive/picker. The
        // workspaceId + userId pair ride along in the state
        // parameter so the callback can resume the user session.
        string state = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{workspaceId:N}:{userId:N}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        string url = "https://accounts.google.com/o/oauth2/v2/auth"
            + $"?client_id={Uri.EscapeDataString(clientId)}"
            + "&response_type=code"
            + "&scope=" + Uri.EscapeDataString("https://www.googleapis.com/auth/drive.readonly")
            + "&access_type=offline&include_granted_scopes=true"
            + "&prompt=consent"
            + $"&state={state}"
            + "&redirect_uri=" + Uri.EscapeDataString(GetRedirectUri());

        return Task.FromResult(Result.Success(url));
    }

    public async Task<Result<AttachmentId>> AttachFileAsync(
        Guid cardId, string fileId, string? fileName, Guid userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            return Result.Failure<AttachmentId>(DomainError.Validation(
                "google_drive.file_id_required", "Google Drive file id is required."));
        }

        GoogleDriveConnection? connection =
            await _connections.FindForUserAsync(new UserId(userId), ct);
        if (connection is null || !connection.Active)
        {
            return Result.Failure<AttachmentId>(DomainError.External(
                "google_drive.not_connected",
                "Google Drive is not connected for this user."));
        }

        string refreshToken;
        try
        {
            refreshToken = _secretProtector.Unprotect(connection.EncryptedRefreshToken);
        }
        catch
        {
            return Result.Failure<AttachmentId>(DomainError.External(
                "google_drive.refresh_token_corrupt",
                "Stored Google Drive refresh token is corrupt; reconnect the integration."));
        }

        Result<string> accessTokenResult = await ExchangeRefreshTokenAsync(refreshToken, ct);
        if (accessTokenResult.IsFailure)
        {
            return Result.Failure<AttachmentId>(accessTokenResult.Error);
        }

        string accessToken = accessTokenResult.Value;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Drive's `files.get?alt=media` returns the raw bytes
        // for any file the user can read. We also pull the
        // metadata first to get the real file name + mime type.
        GoogleDriveFileMeta? meta;
        try
        {
            meta = await _http.GetFromJsonAsync<GoogleDriveFileMeta>(
                $"{GoogleDriveDownloadBase}{Uri.EscapeDataString(fileId)}"
                + "?fields=name,mimeType,size",
                JsonOptions,
                ct);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<AttachmentId>(DomainError.External(
                "google_drive.metadata_error",
                $"Google Drive metadata fetch failed: {ex.Message}"));
        }

        if (meta is null)
        {
            return Result.Failure<AttachmentId>(DomainError.External(
                "google_drive.metadata_empty",
                "Google Drive metadata fetch returned an empty body."));
        }

        byte[] bytes;
        try
        {
            bytes = await _http.GetByteArrayAsync(
                $"{GoogleDriveDownloadBase}{Uri.EscapeDataString(fileId)}?alt=media", ct);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<AttachmentId>(DomainError.External(
                "google_drive.download_error",
                $"Google Drive download failed: {ex.Message}"));
        }

        Card? card = await _cards.GetByIdAsync(new CardId(cardId), ct);
        if (card is null)
        {
            return Result.Failure<AttachmentId>(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        string resolvedName = !string.IsNullOrWhiteSpace(fileName)
            ? fileName
            : (meta.Name ?? $"gdrive-{fileId}");
        string mime = string.IsNullOrWhiteSpace(meta.MimeType) ? "application/octet-stream" : meta.MimeType;

        string storageKey = $"google_drive/{userId:N}/{fileId}/{Guid.NewGuid():N}";
        using (MemoryStream stream = new(bytes, writable: false))
        {
            await _storage.SaveAsync(storageKey, stream, mime, ct);
        }

        var creation = Attachment.Create(
            AttachmentId.New(),
            new CardId(cardId),
            resolvedName,
            mime,
            bytes.LongLength,
            storageKey,
            userId,
            _clock.UtcNow);
        if (creation.IsFailure)
        {
            return Result.Failure<AttachmentId>(creation.Error);
        }

        // The repository / DbSet is registered through
        // IRepository<Attachment, AttachmentId>; in v1 the
        // attachment is added to the same scope's change
        // tracker indirectly. Save here so the unit of work
        // is consistent with the rest of the application.
        connection.RecordUse(_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(creation.Value.Id);
    }

    private async Task<Result<string>> ExchangeRefreshTokenAsync(
        string refreshToken, CancellationToken ct)
    {
        string? clientId = _configuration["Integrations:Google:ClientId"];
        string? clientSecret = _configuration["Integrations:Google:ClientSecret"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return Result.Failure<string>(DomainError.External(
                "google_drive.oauth_config_missing",
                "Google OAuth client id / secret are not configured."));
        }

        Dictionary<string, string> form = new()
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        };

        try
        {
            using FormUrlEncodedContent content = new(form);
            using HttpResponseMessage response = await _http.PostAsync(
                GoogleOauthTokenUrl, content, ct);
            GoogleOauthTokenResponse? body = await response.Content
                .ReadFromJsonAsync<GoogleOauthTokenResponse>(JsonOptions, ct);
            if (body is null || string.IsNullOrWhiteSpace(body.AccessToken))
            {
                return Result.Failure<string>(DomainError.External(
                    "google_drive.token_exchange_failed",
                    "Google OAuth refresh-token exchange returned no access token."));
            }

            return Result.Success(body.AccessToken);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<string>(DomainError.External(
                "google_drive.token_exchange_error",
                $"Google OAuth refresh-token exchange failed: {ex.Message}"));
        }
    }

    private string GetRedirectUri()
    {
        string? configured = _configuration["Integrations:Google:RedirectUri"];
        return !string.IsNullOrWhiteSpace(configured)
            ? configured
            : "http://localhost:5291/api/integrations/google/callback";
    }

    private sealed record GoogleDriveFileMeta(string? Name, string? MimeType, long? Size);
    private sealed record GoogleOauthTokenResponse(string? AccessToken, string? TokenType, int? ExpiresIn);
}
