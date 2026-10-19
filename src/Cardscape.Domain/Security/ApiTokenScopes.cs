using Cardscape.Domain.Common;

namespace Cardscape.Domain.Security;

/// <summary>
/// A non-empty set of <see cref="Scope"/> values granted to an
/// <see cref="ApiToken"/> (an AI agent's key). <see cref="Scope.Read"/>
/// covers every read; writes are granted per capability
/// (<see cref="Scope.CardsWrite"/>, <see cref="Scope.CommentsWrite"/>,
/// <see cref="Scope.BoardsWrite"/>, <see cref="Scope.WorkspaceWrite"/>) or all
/// at once by the original <see cref="Scope.Write"/>. The MCP host enforces
/// the appropriate scope centrally before invoking a tool (see ADR 0015).
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

    /// <summary>
    /// True when these scopes allow an operation that requires
    /// <paramref name="required"/>: the exact scope, or <see cref="Scope.Write"/>
    /// for any write capability.
    /// </summary>
    public bool Grants(Scope required) => ScopeExtensions.Grants(Values, required);

    public override string ToString() => string.Join(';', Values.Select(v => v.ToWire()));
}

/// <summary>The scopes an MCP tool or REST endpoint may require.</summary>
public enum Scope
{
    /// <summary>Every read-only operation.</summary>
    Read = 1,

    /// <summary>Every write capability below (the original all-or-nothing scope).</summary>
    Write = 2,

    /// <summary>Cards, checklists, votes, snoozes, recurrence, card labels and field values, inbox.</summary>
    CardsWrite = 3,

    /// <summary>Adding, editing and deleting comments.</summary>
    CommentsWrite = 4,

    /// <summary>Boards, lists, labels, custom-field definitions, automation, dashcards, extensions, imports.</summary>
    BoardsWrite = 5,

    /// <summary>Invitations, OAuth apps and integrations.</summary>
    WorkspaceWrite = 6,
}

public static class ScopeExtensions
{
    /// <summary>The write capabilities an agent key can be granted one by one.</summary>
    public static IReadOnlyList<Scope> WriteCapabilities { get; } =
        [Scope.CardsWrite, Scope.CommentsWrite, Scope.BoardsWrite, Scope.WorkspaceWrite];

    /// <summary>True for <see cref="Scope.Write"/> and every write capability.</summary>
    public static bool IsWrite(this Scope scope) => scope != Scope.Read;

    public static string ToWire(this Scope scope) => scope switch
    {
        Scope.Read => "read",
        Scope.Write => "write",
        Scope.CardsWrite => "cards:write",
        Scope.CommentsWrite => "comments:write",
        Scope.BoardsWrite => "boards:write",
        Scope.WorkspaceWrite => "workspace:write",
        _ => scope.ToString().ToLowerInvariant()
    };

    public static bool TryParse(string raw, out Scope scope)
    {
        switch (raw.ToLowerInvariant())
        {
            case "read": scope = Scope.Read; return true;
            case "write": scope = Scope.Write; return true;
            case "cards:write": scope = Scope.CardsWrite; return true;
            case "comments:write": scope = Scope.CommentsWrite; return true;
            case "boards:write": scope = Scope.BoardsWrite; return true;
            case "workspace:write": scope = Scope.WorkspaceWrite; return true;
            default: scope = default; return false;
        }
    }

    /// <summary>
    /// True when <paramref name="granted"/> allows an operation that requires
    /// <paramref name="required"/>: the exact scope, or <see cref="Scope.Write"/>
    /// for any write capability.
    /// </summary>
    public static bool Grants(IEnumerable<Scope> granted, Scope required) =>
        granted.Any(scope => scope == required || (scope == Scope.Write && required.IsWrite()));
}
