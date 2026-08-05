using Cardscape.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace Cardscape.Infrastructure.Email;

/// <summary>
/// Logs the email to the console. In production this is replaced
/// by an SMTP-based implementation behind the same interface.
///
/// SECURITY: the body is NEVER logged at Information level. The
/// body routinely carries PII (recipient name, invitation
/// URLs that encode a one-time secret, future password-reset
/// tokens) and persisting it to a log file or the
/// <c>DatabaseLogSink</c> is a privacy / secrets-in-logs leak.
/// Operators that need the body for a single message can flip
/// <c>Cardscape:Logging:EmailBodies</c> to <c>true</c> at Debug
/// level on a per-request basis; the default is body-less.
/// </summary>
public sealed class ConsoleEmailService(ILogger<ConsoleEmailService> logger) : IEmailService
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        // Length is metadata, not PII. Useful to spot empty /
        // oversized bodies; never logs the content itself.
        int bodyLength = message.Body?.Length ?? 0;
        logger.LogInformation(
            "[email] to={To} subject={Subject} html={IsHtml} bodyLength={BodyLength}",
            message.To,
            message.Subject,
            message.IsHtml,
            bodyLength);
        return Task.CompletedTask;
    }
}
