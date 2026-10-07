using System.Reflection;

namespace Cardscape.Web.Shared;

/// <summary>Product facts shown in the chrome (sidebar footer,
/// auth screens). The version comes from the build (Directory.Build.props),
/// the same source the server diagnostics report, so the two cannot drift.</summary>
public static class AppInfo
{
    public static string Version { get; } = "v" + (typeof(AppInfo).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0");
}
