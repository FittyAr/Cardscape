using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Idempotency;
using Cardscape.Domain.Security;
using Cardscape.Mcp.Authorization;
using ModelContextProtocol;

namespace Cardscape.Mcp.Idempotency;

/// <summary>Applies optional replay protection to every catalogued MCP write tool.</summary>
public static class McpToolIdempotencyPolicy
{
    public const string MetaPropertyName = "idempotencyKey";
    public const string InvalidKeyErrorCode = "mcp.idempotency.key_invalid";

    public static async ValueTask<TResult> InvokeAsync<TResult>(
        string? toolName,
        IDictionary<string, JsonElement>? arguments,
        JsonObject? meta,
        ICurrentUser currentUser,
        IIdempotencyKeyStore store,
        IClock clock,
        Func<ValueTask<TResult>> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (string.IsNullOrWhiteSpace(toolName)
            || !McpToolScopePolicy.RequiredScopes.TryGetValue(toolName, out Scope scope)
            || scope != Scope.Write)
        {
            return await next();
        }

        string? idempotencyKey = ReadKey(meta);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return await next();
        }

        string requestJson = SerializeCanonicalRequest(toolName, arguments);
        return await IdempotencyKeyMiddleware.ExecuteAsync(
            idempotencyKey,
            requestJson,
            currentUser,
            store,
            clock,
            async () => await next(),
            cancellationToken);
    }

    public static string SerializeCanonicalRequest(
        string toolName,
        IDictionary<string, JsonElement>? arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("tool", toolName);
            writer.WritePropertyName("arguments");
            writer.WriteStartObject();
            if (arguments is not null)
            {
                foreach ((string name, JsonElement value) in arguments.OrderBy(
                    pair => pair.Key,
                    StringComparer.Ordinal))
                {
                    writer.WritePropertyName(name);
                    WriteCanonical(writer, value);
                }
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string? ReadKey(JsonObject? meta)
    {
        if (meta is null || !meta.TryGetPropertyValue(MetaPropertyName, out JsonNode? node) || node is null)
        {
            return null;
        }

        if (node is JsonValue value && value.TryGetValue(out string? key))
        {
            return key;
        }

        throw new McpException(
            $"{InvalidKeyErrorCode}: _meta.{MetaPropertyName} must be a string.");
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in value.EnumerateObject().OrderBy(
                    property => property.Name,
                    StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in value.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetRawText());
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value), value.ValueKind, null);
        }
    }
}
