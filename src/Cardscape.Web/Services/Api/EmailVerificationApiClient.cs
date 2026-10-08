using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

/// <summary>Email verification: the banner's status and "resend", and the link landing page.</summary>
public interface IEmailVerificationApiClient
{
    Task<ApiResult<EmailVerificationStatusDto>> GetStatusAsync(CancellationToken ct = default);

    Task<ApiResult> ResendAsync(string? language, CancellationToken ct = default);

    Task<ApiResult> VerifyAsync(string token, CancellationToken ct = default);
}

public sealed class EmailVerificationApiClient(IHttpClientFactory http) : ApiClientBase(http), IEmailVerificationApiClient
{
    public async Task<ApiResult<EmailVerificationStatusDto>> GetStatusAsync(CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync("api/auth/verification", ct);
        return await ReadAsync<EmailVerificationStatusDto>(response, ct);
    }

    public async Task<ApiResult> ResendAsync(string? language, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/auth/verification/resend", new { language }, JsonOptions, ct);
        return await ReadAsync(response, ct);
    }

    public async Task<ApiResult> VerifyAsync(string token, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/auth/verify-email", new { token }, JsonOptions, ct);
        return await ReadAsync(response, ct);
    }
}
