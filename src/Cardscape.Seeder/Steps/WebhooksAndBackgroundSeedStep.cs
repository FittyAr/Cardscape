using Cardscape.Domain.BackgroundJobs;
using Cardscape.Domain.Idempotency;
using Cardscape.Domain.Webhooks;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;

namespace Cardscape.Seeder.Steps;

/// <summary>Webhook endpoints + deliveries, plus a handful of
/// background-job and idempotency-key rows so the ops views
/// have something to display.</summary>
internal sealed class WebhooksAndBackgroundSeedStep : SeedStepBase
{
    public override string Name => "Webhooks + background jobs + idempotency";
    public override int Order => 130;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        DateTimeOffset now = context.Now;
        var random = new Random(707);

        // 1. Two webhook endpoints per board (one for the
        //    internal automation bus, one for an external
        //    listener).
        foreach (Board board in context.Boards)
        {
            string secret = Generators.PasswordGenerator.RandomUrlSafeToken(32);
            string secretHash = Generators.PasswordGenerator.Sha256Hex(secret);

            string[] urls =
            {
                // The SSRF guard rejects anything whose host
                // does not resolve via DNS, so the demo
                // endpoints use real, public hosts. The
                // secret hash is what the dispatcher
                // verifies on every call, not the URL
                // itself, so pointing the demo at public
                // hosts is safe in a development
                // environment.
                "https://example.com/cardscape/webhook",
                "https://httpbin.org/post"
            };

            foreach (string url in urls)
            {
                try
                {
                    Result<WebhookEndpoint> endpoint = WebhookEndpoint.Create(
                        WebhookEndpointId.New(),
                        board.Id,
                        url,
                        secretHash,
                        string.Join(",", WebhookEventTypes.All),
                        now);
                    if (endpoint.IsFailure)
                    {
                        // The URL validator rejects loopback /
                        // private addresses (SSRF guard). The
                        // demo URLs are intentionally public, so
                        // this should not happen — log and move
                        // on if it does.
                        Log(log, SeedLogLevel.Warning, $"  ! webhook url {url} rejected: {endpoint.Error.Message}");
                        continue;
                    }

                    context.Add(endpoint.Value);
                    context.WebhookEndpoints.Add(endpoint.Value);

                    // 1-2 deliveries per endpoint so the audit
                    // history is not empty.
                    int deliveryCount = random.Next(1, 3);
                    for (int d = 0; d < deliveryCount; d++)
                    {
                        string eventType = WebhookEventTypes.All[random.Next(0, WebhookEventTypes.All.Count)];
                        string payload = $"{{\"event\":\"{eventType}\",\"ts\":\"{now.AddMinutes(-random.Next(0, 600)):o}\",\"boardId\":\"{board.Id.Value:D}\"}}";
                        Result<WebhookDelivery> delivery = WebhookDelivery.Create(
                            WebhookDeliveryId.New(),
                            endpoint.Value.Id, eventType, payload, now.AddMinutes(-random.Next(0, 600)));
                        if (delivery.IsSuccess)
                        {
                            // Mark a couple as failed / dead-lettered
                            // for visual variety.
                            int roll = random.Next(0, 4);
                            if (roll == 0)
                            {
                                delivery.Value.MarkSuccess(now);
                            }
                            else if (roll == 1)
                            {
                                delivery.Value.MarkFailed("HTTP 502 from upstream", now);
                            }
                            else
                            {
                                delivery.Value.MarkDeadLettered("Max attempts reached", now);
                            }

                            context.Add(delivery.Value);
                            context.WebhookDeliveries.Add(delivery.Value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log(log, SeedLogLevel.Warning, $"  ! webhook url {url} threw: {ex.Message}");
                }
            }
        }

        // 2. Background jobs: a mix of pending / running /
        //    completed / failed / dead-lettered, so the
        //    background-jobs page renders something
        //    interesting.
        string[] jobTypes =
        {
            "card-repeater:spawn",
            "webhook-delivery:deliver",
            "retention-sweeper:purge",
            "scim:sync-group"
        };

        for (int i = 0; i < 12; i++)
        {
            string type = jobTypes[i % jobTypes.Length];
            DateTimeOffset scheduled = now.AddMinutes(-random.Next(0, 600));
            Result<BackgroundJob> job = BackgroundJob.Enqueue(
                type, "{\"seed\":true}", scheduled, 5, now.AddMinutes(-random.Next(0, 700)));
            if (job.IsFailure)
            {
                continue;
            }

            int status = i % 5;
            if (status == 0)
            {
                // Pending
            }
            else if (status == 1)
            {
                job.Value.TryClaim(now);
            }
            else if (status == 2)
            {
                job.Value.TryClaim(now);
                job.Value.MarkCompleted(now);
            }
            else if (status == 3)
            {
                job.Value.TryClaim(now);
                job.Value.MarkFailed("Transient upstream error", now);
            }
            else
            {
                for (int k = 0; k < 5; k++)
                {
                    job.Value.TryClaim(now);
                    job.Value.MarkFailed("Persistent upstream error", now);
                }
            }

            context.Db.BackgroundJobs.Add(job.Value);
            context.BackgroundJobs.Add(job.Value);
        }

        // 3. Idempotency keys: three rows. Each has a
        //    plausible request hash and a 200 response.
        for (int i = 0; i < 3; i++)
        {
            string keyText = $"seed-key-{i}-{Guid.NewGuid():N}";
            string reqHash = Generators.PasswordGenerator.Sha256Hex($"{{\"path\":\"/api/test/{i}\"}}");
            string response = $"{{\"ok\":true,\"echo\":{i}}}";
            Result<IdempotencyKey> idem = IdempotencyKey.Record(
                context.Users[0].Id,
                IdempotencyKeyValue.Create(keyText).Value,
                reqHash,
                200,
                response,
                now.AddMinutes(-random.Next(0, 1440)));
            if (idem.IsSuccess)
            {
                context.Db.IdempotencyKeys.Add(idem.Value);
                context.IdempotencyKeys.Add(idem.Value);
            }
        }

        Log(log, SeedLogLevel.Success,
            $"Inserted {context.WebhookEndpoints.Count} webhook endpoints, {context.WebhookDeliveries.Count} deliveries, " +
            $"{context.BackgroundJobs.Count} background jobs, and {context.IdempotencyKeys.Count} idempotency keys.");
        return Task.CompletedTask;
    }
}
