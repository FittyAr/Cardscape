namespace Cardscape.Application.Abstractions.Email;

/// <summary>
/// Sends transactional email (invitations, password resets, etc.).
/// In Development the implementation just logs to the console; in
/// Production it enqueues the message for a real SMTP provider.
/// </summary>
public interface IEmailService
{
    /// <summary>Sends an email message.</summary>
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>A simple email message envelope.</summary>
public sealed record EmailMessage(
    string To,
    string Subject,
    string Body,
    bool IsHtml = true);
