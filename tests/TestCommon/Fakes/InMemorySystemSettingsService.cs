using Cardscape.Application.Abstractions.Settings;
using Cardscape.Contracts.Settings;
using Cardscape.Domain.Common;

namespace Cardscape.Tests.Common.Fakes;

/// <summary>In-memory <see cref="ISystemSettingsService"/> with the same validation and secret semantics as the real one.</summary>
public sealed class InMemorySystemSettingsService(SystemSettings? initial = null, string? aiApiKey = null) : ISystemSettingsService
{
    private SystemSettings _settings = (initial ?? new SystemSettings()).DeepCopy();
    private string? _aiApiKey = aiApiKey;

    public Task<SystemSettings> GetAsync(CancellationToken ct = default) => Task.FromResult(Public());

    public Task<Result<SystemSettings>> UpdateAsync(SystemSettings settings, string? updatedBy, CancellationToken ct = default)
    {
        if (settings.Validate() is { Count: > 0 } errors)
        {
            return Task.FromResult(Result.Failure<SystemSettings>(
                DomainError.Validation("settings.invalid", errors[0].ErrorMessage ?? "Invalid settings.")));
        }

        _aiApiKey = settings.Ai.ApiKey switch
        {
            null => _aiApiKey,
            "" => null,
            string key => key,
        };
        _settings = settings.DeepCopy();
        return Task.FromResult(Result.Success(Public()));
    }

    public Task<SystemSettings> ResetAsync(string? resetBy, CancellationToken ct = default)
    {
        _settings = new SystemSettings();
        _aiApiKey = null;
        return Task.FromResult(Public());
    }

    public Task<string?> GetAiApiKeyAsync(CancellationToken ct = default) => Task.FromResult(_aiApiKey);

    private SystemSettings Public()
    {
        SystemSettings copy = _settings.DeepCopy();
        copy.Ai.ApiKey = null;
        copy.Ai.HasApiKey = _aiApiKey is not null;
        return copy;
    }
}
