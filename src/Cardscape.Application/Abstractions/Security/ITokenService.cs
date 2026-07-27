using Cardscape.Domain.Members;

namespace Cardscape.Application.Abstractions.Security;

/// <summary>JWT token issuance and validation. Symmetric HMAC for now.</summary>
public interface ITokenService
{
    /// <summary>Issues a short-lived access token for a user.</summary>
    string IssueAccessToken(User user, IReadOnlyCollection<string> roles);

    /// <summary>Issues a long-lived refresh token (opaque, not a JWT).</summary>
    RefreshToken IssueRefreshToken();

    /// <summary>Returns the principal of a JWT, or <c>null</c> if invalid.</summary>
    Guid? GetUserIdFromToken(string token);
}

/// <summary>Opaque refresh token with metadata.</summary>
public sealed record RefreshToken(string Token, DateTimeOffset ExpiresAt);
