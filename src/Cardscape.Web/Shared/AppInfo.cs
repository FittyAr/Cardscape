namespace Cardscape.Web.Shared;

/// <summary>Product facts shown in the chrome (sidebar footer,
/// auth screens). One constant so the two places cannot drift
/// apart again (they used to say v1.2.0 and v1.0.0).</summary>
public static class AppInfo
{
    public const string Version = "v1.2.0";
}
