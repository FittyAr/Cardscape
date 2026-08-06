using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Webhooks;
using Cardscape.Domain.Common;
using Cardscape.Domain.Webhooks;
using Cardscape.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cardscape.Infrastructure.BackgroundJobs;

/// <summary>Background job handler that posts a single webhook
/// delivery. The dispatcher claims a job whose
/// <c>BackgroundJob.Type</c> matches
/// <see cref="WebhookJobTypes.DeliverWebhook"/>, hands it here,
/// and we sign + POST the payload. Success flips the
/// <see cref="WebhookDelivery"/> to <c>Success</c>; any non-2xx or
/// transport error throws so the existing backoff path in
/// <c>BackgroundJob.MarkFailed</c> retries us automatically (5s,
/// 10s, 20s, 40s, 80s, then dead-letter).</summary>
public sealed class WebhookDeliveryHandler : IBackgroundJobHandler
{
    /// <summary>HTTP client timeout for a single delivery attempt.</summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    public string Type => WebhookJobTypes.DeliverWebhook;

    private static readonly JsonSerializerOptions LogJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<WebhookDeliveryHandler> _logger;

    public WebhookDeliveryHandler(
        IServiceScopeFactory scopes,
        ILogger<WebhookDeliveryHandler> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    /// <summary>Reused per-instance; the handler is a singleton,
    /// so this is the connection pool.</summary>
    private static readonly HttpClient SharedHttp = new()
    {
        Timeout = RequestTimeout,
        DefaultRequestHeaders = { UserAgent = { new ProductInfoHeaderValue("Cardscape-Webhooks", "0.7") } }
    };

    public async Task HandleAsync(Guid jobId, JsonElement payload, CancellationToken ct)
    {
        Guid deliveryId = ReadGuid(payload, "deliveryId");

        using IServiceScope scope = _scopes.CreateScope();
        var deliveries = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryRepository>();
        var endpoints = scope.ServiceProvider.GetRequiredService<IWebhookEndpointRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        WebhookDelivery? delivery = await deliveries.GetByIdAsync(
            new WebhookDeliveryId(deliveryId), ct);
        if (delivery is null)
        {
            _logger.LogWarning(
                "Webhook delivery {DeliveryId} not found; skipping.", deliveryId);
            return;
        }

        WebhookEndpoint? endpoint = await endpoints.GetByIdAsync(delivery.EndpointId, ct);
        if (endpoint is null)
        {
            _logger.LogWarning(
                "Webhook endpoint for delivery {DeliveryId} not found; dead-lettering.",
                deliveryId);
            delivery.MarkDeadLettered("Endpoint not found.", clock.UtcNow);
            await unitOfWork.SaveChangesAsync(ct);
            return;
        }

        if (!endpoint.Active)
        {
            // The endpoint was disabled between enqueue and
            // dispatch. Mark the delivery as dead-lettered
            // (it won't be retried).
            delivery.MarkDeadLettered("Endpoint is disabled.", clock.UtcNow);
            await unitOfWork.SaveChangesAsync(ct);
            return;
        }

        if (!endpoint.SubscribesTo(delivery.EventType))
        {
            delivery.MarkDeadLettered(
                $"Endpoint no longer subscribes to '{delivery.EventType}'.",
                clock.UtcNow);
            await unitOfWork.SaveChangesAsync(ct);
            return;
        }

        // Defence-in-depth SSRF check. WebhookEndpoint.Create
        // and ChangeUrl already validate the URL against
        // WebhookUrlValidator, so a freshly-registered
        // endpoint never sees an internal host. The
        // re-check at delivery time closes a DNS
        // rebinding window: a public hostname whose A
        // record is swapped to 127.0.0.1 (or a cloud
        // metadata IP) between enqueue and dispatch
        // would otherwise let the handler POST the
        // signed payload to an internal service. The
        // check is cheap (one DNS resolution per
        // delivery) and the cost is amortised by the
        // 10-second HTTP timeout the handler already
        // has. The pairing recommendation in the
        // WebhookUrlValidator doc comment (outbound IP
        // pinning at the SocketsHttpHandler) is the
        // production-grade defence; this re-check is
        // the in-process belt-and-braces.
        if (Uri.TryCreate(endpoint.Url, UriKind.Absolute, out Uri? parsed))
        {
            Cardscape.Domain.Common.Result ssrfCheck =
                Cardscape.Domain.Webhooks.WebhookUrlValidator.ValidateNotInternalHost(parsed);
            if (ssrfCheck.IsFailure)
            {
                delivery.MarkDeadLettered(
                    $"URL no longer resolves to a public address: {ssrfCheck.Error.Message}",
                    clock.UtcNow);
                await unitOfWork.SaveChangesAsync(ct);
                _logger.LogWarning(
                    "Webhook delivery {DeliveryId} dead-lettered by SSRF re-check: {Reason}",
                    delivery.Id.Value, ssrfCheck.Error.Message);
                return;
            }
        }

        DateTimeOffset now = clock.UtcNow;
        try
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(delivery.PayloadJson);
            string signature = SignBody(endpoint.SecretHash, bodyBytes);

            using HttpRequestMessage request = new(HttpMethod.Post, endpoint.Url)
            {
                Content = new ByteArrayContent(bodyBytes)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" } }
                }
            };
            request.Headers.TryAddWithoutValidation("X-Cardscape-Signature", signature);
            request.Headers.TryAddWithoutValidation("X-Cardscape-Event", delivery.EventType);
            request.Headers.TryAddWithoutValidation("X-Cardscape-Delivery", delivery.Id.Value.ToString());

            using HttpResponseMessage response = await SharedHttp.SendAsync(
                request, HttpCompletionOption.ResponseContentRead, ct);

            if (response.IsSuccessStatusCode)
            {
                delivery.MarkSuccess(now);
                await unitOfWork.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "Delivered webhook {DeliveryId} ({Event}) to {Url} status {Status}.",
                    delivery.Id.Value, delivery.EventType, endpoint.Url, (int)response.StatusCode);
                return;
            }

            string body = await ReadBodySafeAsync(response, ct);
            throw new HttpRequestException(
                $"Webhook endpoint returned {(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(body, 500)}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The BackgroundJob has its own Attempts counter; we
            // share the same 5-attempt cap so a delivery's audit
            // trail matches the underlying job's lifecycle.
            bool willDeadLetter = delivery.AttemptCount + 1 >= BackgroundJobMaxAttempts;
            if (willDeadLetter)
            {
                delivery.MarkDeadLettered(ex.Message, now);
            }
            else
            {
                delivery.MarkFailed(ex.Message, now);
            }

            await unitOfWork.SaveChangesAsync(ct);
            _logger.LogWarning(ex,
                "Webhook delivery {DeliveryId} attempt {Attempt} failed (deadLetter={WillDeadLetter}).",
                delivery.Id.Value, delivery.AttemptCount, willDeadLetter);
            throw;
        }
    }

    /// <summary>Mirror of <c>BackgroundJob.MaxAttempts</c> default.
    /// Kept in sync by convention; the BackgroundJob dispatcher
    /// is the source of truth for the actual retry budget.</summary>
    private const int BackgroundJobMaxAttempts = 5;

    /// <summary>HMAC-SHA256 of <paramref name="body"/> keyed by
    /// <paramref name="secretHash"/>. Returned in the
    /// <c>X-Cardscape-Signature: sha256=&lt;hex&gt;</c> header
    /// shape.</summary>
    public static string SignBody(string secretHash, ReadOnlySpan<byte> body)
    {
        // The secret is stored as a hex SHA-256 of the cleartext.
        // Subscribers know the cleartext; they reproduce the hex
        // hash and use that as the HMAC key. This keeps the DB row
        // safe even if leaked (an attacker would need to recover
        // the cleartext to forge signatures).
        byte[] keyBytes = Convert.FromHexString(secretHash);
        Span<byte> signature = stackalloc byte[32];
        HMACSHA256.HashData(keyBytes, body, signature);
        return "sha256=" + Convert.ToHexString(signature).ToLowerInvariant();
    }

    private static Guid ReadGuid(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out JsonElement el) || !el.TryGetGuid(out Guid id))
        {
            throw new InvalidOperationException(
                $"Webhook delivery job payload missing or invalid '{name}'.");
        }

        return id;
    }

    private static async Task<string> ReadBodySafeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
