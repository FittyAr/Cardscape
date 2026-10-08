using Cardscape.Domain.Common;

namespace Cardscape.Application.Abstractions.Email;

/// <summary>
/// Outbound email transport. Implementations read the SMTP settings at send
/// time, so changes in System settings apply without a restart. Callers
/// check <see cref="Contracts.Settings.EmailSettings.CanSend"/> first and
/// treat a failure as "deliver the link by hand", never as an error of the
/// action that triggered the email.
/// </summary>
public interface IEmailSender
{
    /// <summary>Sends one message; transport errors come back as a failed result, not an exception.</summary>
    Task<Result> SendAsync(OutboundEmail email, CancellationToken ct);
}

/// <summary>A ready-to-send message with a plain-text and an HTML body.</summary>
public sealed record OutboundEmail(string To, string Subject, string TextBody, string HtmlBody);
