using Cardscape.Domain.Members;

namespace Cardscape.Application.Abstractions.Security;

/// <summary>
/// The application's view of the current user. Resolved from the
/// authenticated principal (JWT in the Web/REST API, API token in
/// the MCP server).
/// </summary>
public interface ICurrentUser
{
    /// <summary>True if a user is authenticated.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Identifier of the current user, or <c>null</c> if anonymous.</summary>
    UserId? Id { get; }

    /// <summary>Email of the current user, or <c>null</c> if anonymous.</summary>
    string? Email { get; }

    /// <summary>Display name of the current user, or <c>null</c> if anonymous.</summary>
    string? DisplayName { get; }

    /// <summary>Roles granted to the current user.</summary>
    IReadOnlyCollection<string> Roles { get; }
}
