using System.Text;
using Cardscape.Domain.Common;
using Cardscape.Domain.Webhooks;

namespace Cardscape.Api.Authentication;

internal static class SamlMetadataReader
{
    internal const string HttpClientName = "SamlMetadata";
    internal const int MaxMetadataBytes = 1024 * 1024;

    internal static async Task<string> DownloadAsync(
        HttpClient httpClient,
        string location,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(location, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("SAML metadata URL must be absolute HTTP(S).");
        }

        Result addressCheck = WebhookUrlValidator.ValidateNotInternalHost(uri);
        if (addressCheck.IsFailure)
        {
            throw new InvalidOperationException(addressCheck.Error.Message);
        }

        using HttpResponseMessage response = await httpClient.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await ReadResponseAsync(response, cancellationToken);
    }

    internal static async Task<string> ReadResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > MaxMetadataBytes)
        {
            throw new InvalidOperationException("SAML metadata exceeds the 1 MiB limit.");
        }

        await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[16 * 1024];
        while (true)
        {
            int count = await source.ReadAsync(chunk, cancellationToken);
            if (count == 0)
            {
                break;
            }

            if (buffer.Length + count > MaxMetadataBytes)
            {
                throw new InvalidOperationException("SAML metadata exceeds the 1 MiB limit.");
            }

            buffer.Write(chunk, 0, count);
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length));
    }
}
