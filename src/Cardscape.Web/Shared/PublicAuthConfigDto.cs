namespace Cardscape.Web.Shared;

public sealed record PublicAuthConfigDto(
    bool Google = false,
    bool Microsoft = false,
    bool Apple = false,
    bool GitHub = false,
    bool Saml = false,
    bool AllowPublicRegistration = true,
    string InstanceTitle = "Cardscape",
    string? CustomLogoUrl = null,
    bool MaintenanceModeEnabled = false,
    string? MaintenanceModeMessage = null,
    bool SystemAnnouncementEnabled = false,
    string? SystemAnnouncementType = null,
    string? SystemAnnouncementMessage = null);
