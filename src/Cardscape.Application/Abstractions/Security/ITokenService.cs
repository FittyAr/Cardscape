using Cardscape.Domain.Members;

namespace Cardscape.Application.Abstractions.Security;

/// <summary>JWT token issuance and validation. Symmetric HMAC for now.</summary>
public interface ITokenService
{
    /// <summary>Issues a short-lived access token for a user.</summary>
    string IssueAccessToken(User user, IReadOnlyCollection<string> roles);

}
