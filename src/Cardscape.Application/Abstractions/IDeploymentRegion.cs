using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Abstractions;

/// <summary>
/// Exposes the deployment's configured data-residency region.
/// Wired in <c>DependencyInjection</c> from
/// <c>Cardscape:Deployment:Region</c> configuration; the default
/// implementation returns <see cref="Region.Unspecified"/>
/// (no gating).
/// </summary>
public interface IDeploymentRegion
{
    /// <summary>The deployment's pinned region. <see cref="Region.Unspecified"/>
    /// when the deployment is single-region / dev mode and accepts any
    /// workspace region.</summary>
    Region Region { get; }
}
