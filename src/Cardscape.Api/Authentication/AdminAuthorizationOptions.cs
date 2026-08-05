namespace Cardscape.Api.Authentication;

/// <summary>
/// Operator-facing knobs for the
/// <see cref="AdminOnlyAuthorizationHandler"/>. Bound from the
/// <c>Cardscape:Api:AdminAuthorization</c> configuration
/// section. See <c>docs/operations/06-configurable-subsystems.md</c>
/// for the rationale behind each option.
/// </summary>
public sealed class AdminAuthorizationOptions
{
    public const string SectionName = "Cardscape:Api:AdminAuthorization";

    /// <summary>
    /// When <c>true</c> (the default), the handler reads the
    /// <c>is_admin</c> claim embedded in the JWT at mint
    /// time and only hits the database if the claim is
    /// absent (e.g. a pre-v1.2.0 token still in circulation).
    /// When <c>false</c>, the handler ALWAYS reads
    /// <c>users.IsAdmin</c> from the database. The trade-off:
    /// <list type="bullet">
    ///   <item><c>true</c>: zero DB lookups on the hot
    ///         path; revoking or granting admin requires the
    ///         affected user to re-authenticate (their
    ///         existing access token still encodes the
    ///         previous status until it expires — default
    ///         60 minutes).</item>
    ///   <item><c>false</c>: every admin check is a single
    ///         row seek; admin status changes take effect on
    ///         the next request. Recommended for
    ///         high-compliance deployments where an admin
    ///         revocation must be immediate.</item>
    /// </list>
    /// </summary>
    public bool CacheAdminClaim { get; set; } = true;
}
