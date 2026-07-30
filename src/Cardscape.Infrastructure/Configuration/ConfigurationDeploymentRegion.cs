using Cardscape.Application.Abstractions;
using Cardscape.Domain.Workspaces;
using Microsoft.Extensions.Configuration;

namespace Cardscape.Infrastructure.Configuration;

/// <summary>
/// Reads the deployment's pinned data-residency region from
/// configuration. Source key: <c>Cardscape:Deployment:Region</c>
/// (case-insensitive; the string is parsed to
/// <see cref="Region"/> via <c>Enum.TryParse</c>).
/// An unparseable value or a missing key returns
/// <see cref="Region.Unspecified"/> (no gating).
/// </summary>
public sealed class ConfigurationDeploymentRegion : IDeploymentRegion
{
    public ConfigurationDeploymentRegion(IConfiguration configuration)
    {
        string? raw = configuration["Cardscape:Deployment:Region"]
            ?? configuration["Deployment:Region"];

        Region = TryParse(raw);
    }

    public Region Region { get; }

    private static Region TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Region.Unspecified;
        }

        return Enum.TryParse<Region>(raw, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : Region.Unspecified;
    }
}
