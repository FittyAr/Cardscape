using System.Security.Claims;
using Cardscape.Domain.Security;

namespace Cardscape.Application.Abstractions.Security;

/// <summary>The AI agent acting for the current user: the agent key's id and name.</summary>
public sealed record ActingAgent(Guid Id, string Name);

/// <summary>
/// Builds and reads the principal of a request made with an agent key (an
/// <see cref="ApiToken"/>). The principal is the key's owner, so every
/// Application role check runs exactly as for the owner; the
/// <see cref="TokenIdClaim"/> and <see cref="AgentNameClaim"/> claims mark it
/// as "the owner's AI" so persistence can attribute the change (ADR 0015).
/// </summary>
public static class AgentIdentity
{
    public const string TokenIdClaim = "token_id";
    public const string AgentNameClaim = "agent_name";
    public const string ScopeClaim = "scope";

    /// <summary>
    /// The principal for a validated agent key. When the administrator has
    /// turned agent writes off (<paramref name="writesAllowed"/> false) every
    /// write scope is dropped, so the key is read-only on every surface.
    /// </summary>
    public static ClaimsPrincipal CreatePrincipal(string scheme, ApiTokenValidation validation, bool writesAllowed)
    {
        ArgumentNullException.ThrowIfNull(validation);

        ClaimsIdentity identity = new(scheme, ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, validation.UserId.Value.ToString()));
        identity.AddClaim(new Claim(TokenIdClaim, validation.TokenId.Value.ToString()));
        if (!string.IsNullOrWhiteSpace(validation.Name))
        {
            identity.AddClaim(new Claim(AgentNameClaim, validation.Name));
        }

        foreach (string scope in validation.Scopes)
        {
            if (!writesAllowed && (!ScopeExtensions.TryParse(scope, out Scope parsed) || parsed.IsWrite()))
            {
                continue;
            }

            identity.AddClaim(new Claim(ScopeClaim, scope));
        }

        return new ClaimsPrincipal(identity);
    }

    /// <summary>The agent acting for the current user, or <c>null</c> when the person acts directly.</summary>
    public static ActingAgent? ActingAgent(this ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        if (!currentUser.IsAuthenticated
            || !Guid.TryParse(currentUser.FindFirst(TokenIdClaim), out Guid tokenId))
        {
            return null;
        }

        string? name = currentUser.FindFirst(AgentNameClaim);
        return new ActingAgent(tokenId, string.IsNullOrWhiteSpace(name) ? "AI agent" : name);
    }

    /// <summary>
    /// The scopes granted to the principal. Only the exact wire form counts
    /// (<c>read</c>, not <c>READ</c>); anything else is ignored.
    /// </summary>
    public static IReadOnlyList<Scope> GrantedScopes(ClaimsPrincipal? principal) =>
        principal?.Identity?.IsAuthenticated == true
            ? [.. principal.FindAll(ScopeClaim)
                .Select(claim => ScopeExtensions.TryParse(claim.Value, out Scope scope)
                    && string.Equals(scope.ToWire(), claim.Value, StringComparison.Ordinal)
                        ? scope
                        : (Scope?)null)
                .OfType<Scope>()]
            : [];
}
