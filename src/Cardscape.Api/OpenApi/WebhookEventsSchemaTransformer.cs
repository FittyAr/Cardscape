using Cardscape.Api.Logging;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Cardscape.Api.OpenApi;

/// <summary>
/// Adds a <c>WebhookEvent</c> schema to the OpenAPI document so
/// integrators can discover the event identifiers the platform
/// will accept. The values are frozen in
/// <c>Cardscape.Domain.Webhooks.WebhookEventTypes.All</c>;
/// re-exporting them in the OpenAPI document keeps the doc
/// honest (the previous incarnation didn't document the enum
/// at all and a naive integrator could send <c>card.updated</c>
/// — which is not a real event — and only learn that at
/// runtime via a 400).
///
/// BETA-8-API-#4 — see <c>test-results/r8/r8-report.md</c>.
/// </summary>
internal sealed class WebhookEventsSchemaTransformer : IOpenApiDocumentTransformer
{
    private static readonly string[] Events =
    {
        "card.created",
        "card.moved",
        "card.completed",
        "comment.added"
    };

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var logger = context.ApplicationServices.GetService<ILoggerFactory>()?.CreateLogger("WebhookEvents");
        logger?.WebhookEventsSchemaTransformerRunning();

        document.Components ??= new OpenApiComponents();
        if (document.Components.Schemas is null)
        {
            document.Components.Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        }
        else if (document.Components.Schemas is not IDictionary<string, IOpenApiSchema>)
        {
            // Rebuild so the index-set below is allowed; the
            // framework exposes a read-only view in some versions.
            document.Components.Schemas = new Dictionary<string, IOpenApiSchema>(
                document.Components.Schemas, StringComparer.Ordinal);
        }

        // The framework prunes schemas that are not referenced by
        // any operation. So we don't just register WebhookEvent;
        // we also rewrite the `events` field on CreateWebhookBody
        // to $ref WebhookEvent, which both surfaces the enum in
        // the rendered doc and keeps the schema alive.
        OpenApiSchemaReference eventRef = new("WebhookEvent");
        ReplaceEventsField(document, "CreateWebhookBody", eventRef);

        // Exposed as a string enum so Kiota / openapi-typescript /
        // NSwag all map it to a string union on the client. The
        // description lists every value so the integrated SDK
        // also surfaces them in IntelliSense.
        ((IDictionary<string, IOpenApiSchema>)document.Components.Schemas)["WebhookEvent"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "Event identifier the webhook subscribes to. "
                + "Frozen list; sending a value outside the enum returns 400. "
                + "Allowed values: " + string.Join(", ", Events) + "."
        };

        return Task.CompletedTask;
    }

    private static void ReplaceEventsField(OpenApiDocument document, string bodySchemaName, OpenApiSchemaReference eventRef)
    {
        if (document.Components?.Schemas is null)
        {
            return;
        }
        if (!document.Components.Schemas.TryGetValue(bodySchemaName, out IOpenApiSchema? raw))
        {
            return;
        }
        if (raw is not OpenApiSchema schema)
        {
            return;
        }
        if (schema.Properties is null || !schema.Properties.TryGetValue("events", out IOpenApiSchema? eventsField))
        {
            return;
        }
        // The existing `events` is a bare string array; wrap it so
        // the items reference WebhookEvent. Both array shape and
        // item type are kept (clients can still send a list of
        // strings, just a constrained one).
        OpenApiSchema replacement = new()
        {
            Type = JsonSchemaType.Array,
            Items = eventRef
        };
        if (schema.Properties is Dictionary<string, IOpenApiSchema> mutable)
        {
            mutable["events"] = replacement;
        }
    }
}
