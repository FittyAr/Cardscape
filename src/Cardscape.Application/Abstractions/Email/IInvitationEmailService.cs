namespace Cardscape.Application.Abstractions.Email;

/// <summary>
/// Out-of-band delivery for the cleartext invitation token. The
/// default implementation logs the link to the console so developers
/// can copy it from the API logs during local smoke tests; in
/// production this is replaced by an SMTP / SES / SendGrid adapter
/// that knows the public invite URL of the Web project.
/// </summary>
public interface IInvitationEmailService
{
    /// <summary>
    /// Delivers an invitation to <paramref name="toEmail"/>. The
    /// cleartext token is the secret URL parameter; the service is
    /// free to wrap it in a public-facing URL (e.g.
    ///    <c>https://app.example.com/invitations/accept?token=...</c>)
    /// or send it verbatim, depending on the channel.
    /// </summary>
    Task SendAsync(
        string toEmail,
        string workspaceName,
        string cleartextToken,
        CancellationToken ct = default);
}
