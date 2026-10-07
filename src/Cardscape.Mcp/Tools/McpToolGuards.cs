using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;

namespace Cardscape.Mcp.Tools;

/// <summary>
/// How MCP tools surface failures: the SDK reports a thrown exception to
/// the client as a tool error, so a failed result becomes an
/// <see cref="InvalidOperationException"/> whose message is
/// <c>"code: message"</c>.
/// </summary>
internal static class McpToolGuards
{
    internal const string MissingPrincipalHint = "Pass a Bearer JWT or API token in the Authorization header.";
    internal const string AlternateHint = "Pass the API token as an Authorization: Bearer header to the MCP HTTP endpoint.";

    extension(ICurrentUser currentUser)
    {
        /// <summary>Rejects the call when no principal is authenticated.</summary>
        public void RequireAuthenticated(string hint = MissingPrincipalHint)
        {
            if (!currentUser.IsAuthenticated)
            {
                throw new UnauthorizedAccessException(
                    "MCP tool call rejected: no authenticated principal. " + hint);
            }
        }
    }

    extension(Result result)
    {
        /// <summary>Throws the tool error when the result failed.</summary>
        public void OrThrow()
        {
            if (result.IsFailure)
            {
                throw new InvalidOperationException($"{result.Error.Code}: {result.Error.Message}");
            }
        }
    }

    extension<T>(Result<T> result)
    {
        /// <summary>The value, or the tool error when the result failed.</summary>
        public T OrThrow()
        {
            if (result.IsFailure)
            {
                throw new InvalidOperationException($"{result.Error.Code}: {result.Error.Message}");
            }

            return result.Value!;
        }
    }
}
