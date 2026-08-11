using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cardscape.Tests.Common;

/// <summary>
/// Centralised <see cref="JsonSerializerOptions"/> for the integration test
/// suite. The API emits enums as camelCase strings (the server configures
/// <c>JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)</c>
/// so the wire format is human-readable for MCP, Scalar, and any other
/// non-JSON-aware consumer), so the test <c>HttpClient</c> has to use the
/// same converter on the way back in. The default
/// <c>HttpClientJsonExtensions.ReadFromJsonAsync</c> overloads use no
/// options and therefore reject the string enums with
/// <c>System.Text.Json.JsonException: The JSON value could not be
/// converted to …</c>, which is what the 60 pre-existing
/// <c>region / role / visibility / …</c> integration test failures
/// all came down to.
/// </summary>
/// <remarks>
/// <para>
/// Property naming is left at the default (PascalCase preserved). The
/// API contract uses <see cref="JsonNamingPolicy.CamelCase"/> for its
/// outgoing payloads, but the test code deserialises into the same
/// DTO types the API emits, which already carry
/// <see cref="JsonPropertyNameAttribute"/>s on every field. Letting
/// the deserialiser default to case-insensitive property matching
/// means a missing attribute is still matched against the
/// CLR-side PascalCase name; the camelCase property name in the
/// JSON is found via the attribute. That keeps the test code
/// resilient against future API property renames as long as
/// the <see cref="JsonPropertyNameAttribute"/> is kept in sync.
/// </para>
/// </remarks>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
        }
    };
}
