using Microsoft.Extensions.Configuration;

namespace Cardscape.Infrastructure.Configuration;

/// <summary>
/// The shared secret the API and the MCP server present to each other on
/// their internal broadcast endpoints.
/// </summary>
public static class InternalSecret
{
    /// <summary>Request header that carries the secret.</summary>
    public const string HeaderName = "X-Internal-Secret";

    extension(IConfiguration configuration)
    {
        /// <summary>
        /// The secret an outbound internal call presents: <c>Internal:Secret</c>,
        /// then <c>Cardscape:Internal:Secret</c>, then the
        /// <c>CARDS_CAPE__INTERNAL__SECRET</c> environment variable.
        /// </summary>
        public string? OutboundInternalSecret =>
            configuration["Internal:Secret"]
            ?? configuration["Cardscape:Internal:Secret"]
            ?? Environment.GetEnvironmentVariable("CARDS_CAPE__INTERNAL__SECRET");
    }
}
