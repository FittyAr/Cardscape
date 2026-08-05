namespace Cardscape.Domain.Authentication.RevokedTokens;

/// <summary>
/// Strongly-typed identifier for a revoked-token record.
/// The value is a Guid the repository generates with
/// <see cref="New"/>; the <c>jti</c> JWT claim is the
/// natural key but is not used as the row id because
/// the <c>jti</c> is a string and the database id is
/// a Guid.
/// </summary>
public readonly record struct RevokedTokenId(Guid Value)
{
    public static RevokedTokenId New() => new(Guid.NewGuid());
}
