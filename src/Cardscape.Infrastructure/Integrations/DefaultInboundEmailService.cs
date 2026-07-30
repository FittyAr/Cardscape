using System.Text.Json;
using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Cards.Commands;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Common;
using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.InboundEmail;
using Wolverine;

namespace Cardscape.Infrastructure.Integrations;

/// <summary>
/// Default <see cref="IInboundEmailService"/> that recognises the
/// three providers the project supports out of the box:
///
/// <list type="bullet">
///   <item><b>sendgrid</b> — JSON body with
///         <c>from</c>, <c>subject</c>, <c>text</c>, and
///         <c>envelope[to]</c>.</item>
///   <item><b>mailgun</b> — JSON or form body with
///         <c>sender</c>, <c>subject</c>, <c>body-plain</c>, and
///         <c>recipient</c>.</item>
///   <item><b>postmark</b> — JSON body with
///         <c>FromFull[Email]</c>, <c>Subject</c>,
///         <c>TextBody</c>, and <c>ToFull[*].Email</c>.</item>
/// </list>
///
/// The handler resolves the destination address to an
/// <see cref="InboundEmailAddress"/> row and delegates card
/// creation to the same <c>CreateCardCommand</c> the REST API
/// uses, so authorization, validation, and domain-event side
/// effects (realtime push, Slack, webhooks) all stay in one
/// place.
/// </summary>
public sealed class DefaultInboundEmailService(
    IInboundEmailAddressRepository addresses,
    IMessageBus bus) : IInboundEmailService
{
    public async Task<Result<Guid>> HandleAsync(
        string provider,
        string rawBody,
        IDictionary<string, string> headers,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return Result.Failure<Guid>(DomainError.External(
                "inbound_email.body_empty",
                "Inbound email webhook delivered an empty body."));
        }

        InboundEmailEnvelope? envelope = Parse(provider, rawBody, headers);
        if (envelope is null)
        {
            return Result.Failure<Guid>(DomainError.External(
                "inbound_email.unrecognised",
                $"Unrecognised inbound email provider '{provider}'."));
        }

        if (string.IsNullOrWhiteSpace(envelope.Recipient))
        {
            return Result.Failure<Guid>(DomainError.External(
                "inbound_email.recipient_missing",
                "Inbound email payload is missing the destination address."));
        }

        InboundEmailAddress? address =
            await addresses.FindByEmailAsync(envelope.Recipient, ct);
        if (address is null || !address.Active)
        {
            return Result.Failure<Guid>(DomainError.NotFound(
                "inbound_email.not_registered",
                $"Destination address '{envelope.Recipient}' is not registered."));
        }

        string subject = string.IsNullOrWhiteSpace(envelope.Subject)
            ? "(no subject)"
            : envelope.Subject;
        string body = envelope.TextBody ?? string.Empty;

        var command = new CreateCardCommand(
            address.TargetListId.Value,
            subject,
            body);
        Result<CardDto> result = await bus.InvokeAsync<Result<CardDto>>(command, ct);
        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        return Result.Success(result.Value.Id);
    }

    private static InboundEmailEnvelope? Parse(
        string provider, string rawBody, IDictionary<string, string> headers)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return null;
        }

        string normalised = provider.Trim().ToLowerInvariant();
        try
        {
            return normalised switch
            {
                "sendgrid" => ParseSendGrid(rawBody),
                "mailgun" => ParseMailgun(rawBody),
                "postmark" => ParsePostmark(rawBody),
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static InboundEmailEnvelope? ParseSendGrid(string raw)
    {
        using JsonDocument doc = JsonDocument.Parse(raw);
        JsonElement root = doc.RootElement;
        string recipient = root.TryGetProperty("envelope", out JsonElement envelope)
                           && envelope.TryGetProperty("to", out JsonElement to)
                           && to.ValueKind == JsonValueKind.Array
                           && to.GetArrayLength() > 0
            ? to[0].GetString() ?? string.Empty
            : string.Empty;
        return new InboundEmailEnvelope(
            Recipient: recipient,
            Subject: root.TryGetProperty("subject", out JsonElement subj) ? subj.GetString() : null,
            TextBody: root.TryGetProperty("text", out JsonElement text) ? text.GetString() : null);
    }

    private static InboundEmailEnvelope? ParseMailgun(string raw)
    {
        // Mailgun can deliver either JSON or
        // application/x-www-form-urlencoded. Try JSON first; fall
        // back to the form parser if that fails.
        try
        {
            using JsonDocument doc = JsonDocument.Parse(raw);
            JsonElement root = doc.RootElement;
            return new InboundEmailEnvelope(
                Recipient: root.TryGetProperty("recipient", out JsonElement r) ? r.GetString() ?? string.Empty : string.Empty,
                Subject: root.TryGetProperty("subject", out JsonElement s) ? s.GetString() : null,
                TextBody: root.TryGetProperty("body-plain", out JsonElement b) ? b.GetString() : null);
        }
        catch (JsonException)
        {
            // Form-encoded fallback. We don't pull in
            // Microsoft.AspNetCore.WebUtilities for the
            // form parser; splitting on '&' and '=' is
            // enough for the small handful of fields we
            // care about.
            Dictionary<string, string> form = raw
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('=', 2))
                .ToDictionary(
                    kv => Uri.UnescapeDataString(kv[0]),
                    kv => kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty);
            return new InboundEmailEnvelope(
                Recipient: form.GetValueOrDefault("recipient") ?? string.Empty,
                Subject: form.GetValueOrDefault("subject"),
                TextBody: form.GetValueOrDefault("body-plain"));
        }
    }

    private static InboundEmailEnvelope? ParsePostmark(string raw)
    {
        using JsonDocument doc = JsonDocument.Parse(raw);
        JsonElement root = doc.RootElement;

        string recipient = string.Empty;
        if (root.TryGetProperty("ToFull", out JsonElement toFull)
            && toFull.ValueKind == JsonValueKind.Array
            && toFull.GetArrayLength() > 0
            && toFull[0].TryGetProperty("Email", out JsonElement toEmail))
        {
            recipient = toEmail.GetString() ?? string.Empty;
        }

        return new InboundEmailEnvelope(
            Recipient: recipient,
            Subject: root.TryGetProperty("Subject", out JsonElement subj) ? subj.GetString() : null,
            TextBody: root.TryGetProperty("TextBody", out JsonElement text) ? text.GetString() : null);
    }

    private sealed record InboundEmailEnvelope(
        string Recipient, string? Subject, string? TextBody);
}
