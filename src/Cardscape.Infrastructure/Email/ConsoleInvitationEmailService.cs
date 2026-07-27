using Cardscape.Application.Abstractions.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cardscape.Infrastructure.Email;

/// <summary>
/// Logs the invite URL to the console. In production this is
/// replaced by an SMTP / SES / SendGrid adapter behind the same
/// interface. The public base URL of the Web project is read from
/// <c>App:PublicBaseUrl</c> (default <c>http://localhost:5206</c>,
/// the Blazor WASM port in <c>launchSettings.json</c>).
/// </summary>
public sealed class ConsoleInvitationEmailService(
    IConfiguration configuration,
    ILogger<ConsoleInvitationEmailService> logger) : IInvitationEmailService
{
    public Task SendAsync(
        string toEmail,
        string workspaceName,
        string cleartextToken,
        CancellationToken ct = default)
    {
        var baseUrl = configuration["App:PublicBaseUrl"]?.TrimEnd('/')
            ?? "http://localhost:5206";
        var url = $"{baseUrl}/invitations/accept?token={Uri.EscapeDataString(cleartextToken)}";

        logger.LogInformation(
            "[invite] to={To} workspace={Workspace} url={Url}",
            toEmail, workspaceName, url);

        return Task.CompletedTask;
    }
}
