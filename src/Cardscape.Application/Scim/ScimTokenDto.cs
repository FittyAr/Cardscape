using Cardscape.Domain.Authentication.Scim;

namespace Cardscape.Application.Scim;

public sealed record ScimTokenDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string TokenPrefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    bool IsRevoked)
{
    public static ScimTokenDto FromEntity(ScimToken token) => new(
        token.Id.Value,
        token.WorkspaceId.Value,
        token.Name,
        token.TokenPrefix,
        token.CreatedAt,
        token.LastUsedAt,
        token.IsRevoked);
}
