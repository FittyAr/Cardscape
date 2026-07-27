using Cardscape.Domain.Common;

namespace Cardscape.Domain.Security.Errors;

/// <summary>
/// Domain errors raised by <see cref="ApiToken"/> invariants
/// and lifecycle transitions.
/// </summary>
public static class ApiTokenErrors
{
    public static readonly DomainError AlreadyRevoked = DomainError.Conflict(
        "security.api_token.already_revoked",
        "API token has already been revoked.");
}
