namespace Cardscape.Application.Abstractions.Settings;

public sealed record SystemSettingsDto(
    string InstanceTitle,
    bool AllowPublicRegistration,
    string DefaultLanguage,
    int JwtAccessTokenMinutes,
    string DatabaseProvider,
    string Environment,
    string StorageRoot,
    string AppVersion);

public sealed record UpdateSystemSettingsRequest(
    string InstanceTitle,
    bool AllowPublicRegistration,
    string DefaultLanguage,
    int JwtAccessTokenMinutes);

public interface ISystemSettingsService
{
    Task<SystemSettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task<SystemSettingsDto> UpdateSettingsAsync(UpdateSystemSettingsRequest request, string? updatedBy, CancellationToken ct = default);
    Task<bool> IsPublicRegistrationAllowedAsync(CancellationToken ct = default);
}
