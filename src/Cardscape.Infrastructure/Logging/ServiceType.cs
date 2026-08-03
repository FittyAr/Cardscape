namespace Cardscape.Infrastructure.Logging;

/// <summary>
/// Identifies which host the logger is configured for. The
/// <c>UseCardscapeSerilog</c> extension uses this to pick the
/// right log folder, enrich every event with a <c>Service</c>
/// property, and tag exceptions / OTel resources.
/// </summary>
public enum ServiceType
{
    Api,
    Mcp,
    Web
}
