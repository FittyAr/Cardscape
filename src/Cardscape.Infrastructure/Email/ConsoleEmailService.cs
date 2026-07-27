using Cardscape.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace Cardscape.Infrastructure.Email;

/// <summary>
/// Logs the email to the console. In production this is replaced
/// by an SMTP-based implementation behind the same interface.
/// </summary>
public sealed class ConsoleEmailService(ILogger<ConsoleEmailService> logger) : IEmailService
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[email] to={To} subject={Subject} html={IsHtml} body={Body}",
            message.To,
            message.Subject,
            message.IsHtml,
            message.Body);
        return Task.CompletedTask;
    }
}
