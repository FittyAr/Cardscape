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
    /// When <c>true</c>, the handler reads the
    /// <c>is_admin</c> claim embedded in the JWT at mint
    /// time and fails closed when the claim is absent or is
    /// not exactly <c>true</c>.
    /// When <c>false</c> (the default), the handler ALWAYS reads
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
    ///         the next request. This is the default because
    ///         the instance Users page lets an administrator
    ///         revoke admin, deactivate or delete a user, and
    ///         those changes must lock the user out at once;
    ///         the admin surface is low-traffic, so one
    ///         primary-key seek per request is negligible.</item>
    /// </list>
    /// </summary>
    public bool CacheAdminClaim { get; set; }
}
