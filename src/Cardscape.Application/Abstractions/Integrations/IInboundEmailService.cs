using Cardscape.Domain.Common;

namespace Cardscape.Application.Abstractions.Integrations;

/// <summary>
/// Translates a webhook payload from an inbound email provider
/// (SendGrid, Mailgun, or Postmark) into a domain action: create
/// a new card on the target list. The default implementation
/// reads the SendGrid / Mailgun / Postmark JSON shape; tests
/// can substitute a fake that returns deterministic DTOs.
/// </summary>
public interface IInboundEmailService
{
    /// <summary>
    /// Parses <paramref name="rawBody"/> (the HTTP request body
    /// verbatim) using the provider hint and creates a card on
    /// the target list of the matching
    /// <see cref="Cardscape.Domain.Integrations.InboundEmail.InboundEmailAddress"/>.
    /// Returns the id of the new card on success.
    /// </summary>
    Task<Result<Guid>> HandleAsync(
        string provider,
        string rawBody,
        IDictionary<string, string> headers,
        CancellationToken ct = default);
}
