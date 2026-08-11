using Cardscape.Domain.Common;

namespace Cardscape.Domain.Security;

/// <summary>
/// A non-empty set of <see cref="Scope"/> values granted to an
/// <see cref="ApiToken"/>. The standard set in v0.3 is
/// <see cref="Scope.Read"/> and <see cref="Scope.Write"/>. The MCP host
/// enforces the appropriate scope centrally before invoking a tool.
/// </summary>
public sealed record ApiTokenScopes : IValueObject
{
    public IReadOnlyCollection<Scope> Values { get; }

    private ApiTokenScopes(IReadOnlyCollection<Scope> values) => Values = values;

    public static Result<ApiTokenScopes> Create(IEnumerable<string>? raw)
    {
        if (raw is null)
        {
            return Result.Failure<ApiTokenScopes>(DomainError.Validation(
                "security.api_token.scopes_required",
                "API token must have at least one scope."));
        }

        var normalized = raw
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        if (normalized.Count == 0)
        {
            return Result.Failure<ApiTokenScopes>(DomainError.Validation(
                "security.api_token.scopes_required",
                "API token must have at least one scope."));
        }

        var parsed = new List<Scope>(normalized.Count);
        foreach (var s in normalized)
        {
            if (!ScopeExtensions.TryParse(s, out var scope))
            {
                return Result.Failure<ApiTokenScopes>(DomainError.Validation(
                    "security.api_token.unknown_scope",
                    $"Unknown scope '{s}'."));
            }

            parsed.Add(scope);
        }

        return Result.Success(new ApiTokenScopes(parsed));
    }

    public bool Has(Scope required) => Values.Contains(required);

    public override string ToString() => string.Join(';', Values.Select(v => v.ToString().ToLowerInvariant()));
}

/// <summary>The scopes an MCP tool or REST endpoint may require.</summary>
public enum Scope
{
    Read = 1,
    Write = 2
}

public static class ScopeExtensions
{
    public static string ToWire(this Scope scope) => scope switch
    {
        Scope.Read => "read",
        Scope.Write => "write",
        _ => scope.ToString().ToLowerInvariant()
    };

    public static bool TryParse(string raw, out Scope scope)
    {
        switch (raw.ToLowerInvariant())
        {
            case "read": scope = Scope.Read; return true;
            case "write": scope = Scope.Write; return true;
            default: scope = default; return false;
        }
    }
}
