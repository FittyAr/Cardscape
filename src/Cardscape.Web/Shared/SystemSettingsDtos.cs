namespace Cardscape.Web.Shared;

public sealed record SystemSettingsDto(
    string InstanceTitle,
    bool AllowPublicRegistration,
    string DefaultLanguage,
    int JwtAccessTokenMinutes,
    string DatabaseProvider,
    string Environment,
    string StorageRoot,
    string AppVersion);

public sealed record UpdateSystemSettingsRequestDto(
    string InstanceTitle,
    bool AllowPublicRegistration,
    string DefaultLanguage,
    int JwtAccessTokenMinutes);
