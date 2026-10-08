using Cardscape.Application.Abstractions.Email;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Contracts.Settings;
using Cardscape.Domain.Common;
using Cardscape.Infrastructure.Logging;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Cardscape.Infrastructure.Email;

/// <summary>
/// <see cref="IEmailSender"/> over SMTP (MailKit). Host, port, TLS mode,
/// credentials and sender are read from <see cref="EmailSettings"/> on every
/// send, so changes in System settings apply immediately. One connection per
/// message: Cardscape sends a handful of transactional emails, not bulk mail.
/// </summary>
public sealed class SmtpEmailSender(ISystemSettingsService settings, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public async Task<Result> SendAsync(OutboundEmail email, CancellationToken ct)
    {
        SystemSettings instance = await settings.GetAsync(ct);
        EmailSettings smtp = instance.Email;
        if (!smtp.CanSend())
        {
            return Result.Failure(DomainError.Validation(
                "email.not_configured", "Outbound email is not configured in System settings."));
        }

        string host = smtp.Host!;
        string recipientDomain = email.To[(email.To.LastIndexOf('@') + 1)..];
        try
        {
            using MimeMessage message = new();
            message.From.Add(new MailboxAddress(smtp.FromName ?? instance.General.InstanceTitle, smtp.FromAddress!));
            message.To.Add(MailboxAddress.Parse(email.To));
            message.Subject = email.Subject;
            message.Body = new BodyBuilder { TextBody = email.TextBody, HtmlBody = email.HtmlBody }.ToMessageBody();

            using SmtpClient client = new() { Timeout = (int)Timeout.TotalMilliseconds };
            await client.ConnectAsync(host, smtp.Port, ToSocketOptions(smtp.Security), ct);
            if (!string.IsNullOrWhiteSpace(smtp.Username))
            {
                string password = await settings.GetSmtpPasswordAsync(ct) ?? string.Empty;
                await client.AuthenticateAsync(smtp.Username, password, ct);
            }

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(quit: true, ct);
            InfrastructureEmailLogMessages.EmailSent(logger, email.Subject, recipientDomain, host, smtp.Port);
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Socket, TLS, authentication, protocol and address-parsing
            // failures all mean the same to callers: share the link by hand.
            InfrastructureEmailLogMessages.EmailFailed(logger, email.Subject, recipientDomain, host, smtp.Port, ex);
            return Result.Failure(DomainError.External("email.send_failed", ex.Message));
        }
    }

    private static SecureSocketOptions ToSocketOptions(SmtpSecurity security) => security switch
    {
        SmtpSecurity.None => SecureSocketOptions.None,
        SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.StartTls,
    };
}
