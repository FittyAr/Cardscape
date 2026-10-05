namespace Cardscape.Application.Authentication.DTOs;

public sealed record PublicAuthConfigResponse(
    bool Google,
    bool Microsoft,
    bool Apple,
    bool GitHub,
    bool Saml,
    bool AllowPublicRegistration,
    string InstanceTitle,
    string? CustomLogoUrl,
    bool MaintenanceModeEnabled,
    string? MaintenanceModeMessage,
    bool SystemAnnouncementEnabled,
    string? SystemAnnouncementType,
    string? SystemAnnouncementMessage);
