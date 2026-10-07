using System.Text.Json.Serialization;

namespace Cardscape.Contracts.Settings;

/// <summary>
/// The subset of <see cref="SystemSettings"/> anonymous visitors may read
/// (served by <c>GET /api/auth/config</c>): branding, the sign-in options
/// that are actually usable, notices and which optional features are on.
/// </summary>
public sealed record PublicInstanceSettings(
    string InstanceTitle,
    string? LogoUrl,
    string? SupportEmail,
    string? WelcomeMessage,
    string DefaultLanguage,
    string DefaultTheme,
    bool AllowPublicRegistration,
    bool Google,
    bool Microsoft,
    bool Apple,
    bool Saml,
    string? Announcement,
    NoticeSeverity AnnouncementSeverity,
    string? MaintenanceMessage,
    bool AiEnabled)
{
    [JsonIgnore]
    public bool IsInMaintenance => MaintenanceMessage is not null;
}
