using Cardscape.Application.Abstractions.Email;
using Cardscape.Application.Abstractions.Settings;

namespace Cardscape.Api.Settings;

/// <summary>
/// Absolute links for emails. The administrator's
/// <see cref="Contracts.Settings.EmailSettings.PublicBaseUrl"/> wins; otherwise
/// the scheme, host and path base of the current request are used, which is
/// right when the API serves the Web client on the address people browse to.
/// </summary>
public sealed class PublicLinkBuilder(ISystemSettingsService settings, IHttpContextAccessor http) : IPublicLinkBuilder
{
    public async Task<string?> BuildAsync(string relativePath, CancellationToken ct)
    {
        string? baseUrl = (await settings.GetAsync(ct)).Email.PublicBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl) && http.HttpContext?.Request is { Host.HasValue: true } request)
        {
            baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
        }

        return string.IsNullOrWhiteSpace(baseUrl)
            ? null
            : $"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }
}
