using Cardscape.Contracts.Settings;
using Cardscape.Domain.Common;

namespace Cardscape.Application.Abstractions.Settings;

/// <summary>
/// Store of the instance-wide <see cref="SystemSettings"/>. Reads are cached;
/// writes are validated, persisted atomically and visible to every consumer
/// immediately (no restart).
/// </summary>
public interface ISystemSettingsService
{
    /// <summary>Current settings. Secrets are never included (<see cref="AiSettings.ApiKey"/> is null).</summary>
    Task<SystemSettings> GetAsync(CancellationToken ct = default);

    /// <summary>Validates and stores <paramref name="settings"/>; returns the stored (secret-free) settings.</summary>
    Task<Result<SystemSettings>> UpdateAsync(SystemSettings settings, string? updatedBy, CancellationToken ct = default);

    /// <summary>Restores the defaults (derived from the startup configuration) and returns them.</summary>
    Task<SystemSettings> ResetAsync(string? resetBy, CancellationToken ct = default);

    /// <summary>The stored AI API key in clear text, for the AI client only.</summary>
    Task<string?> GetAiApiKeyAsync(CancellationToken ct = default);
}
