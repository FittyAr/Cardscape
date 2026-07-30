namespace Cardscape.Domain.Workspaces;

/// <summary>
/// Geographic region a workspace's data is stored in. The
/// deployment is configured with a single region (see
/// <c>Cardscape:Deployment:Region</c>); when that is set, the
/// API rejects cross-region writes — a workspace whose
/// <see cref="Region"/> does not match the deployment's region
/// cannot be written to.
/// </summary>
public enum Region
{
    /// <summary>No region assigned. Defaults to the deployment's region
    /// when one is configured; otherwise the workspace is not
    /// region-gated.</summary>
    Unspecified = 0,

    /// <summary>European Union region. Frankfurt, Ireland, Stockholm,
    /// etc. GDPR data-residency boundary.</summary>
    Europe = 1,

    /// <summary>United States / Canada region.</summary>
    NorthAmerica = 2,

    /// <summary>Asia Pacific region. Tokyo, Singapore, Sydney.</summary>
    AsiaPacific = 3,

    /// <summary>South America region. São Paulo, Santiago.</summary>
    SouthAmerica = 4
}
