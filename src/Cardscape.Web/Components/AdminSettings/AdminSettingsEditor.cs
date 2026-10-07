using Cardscape.Contracts.Settings;
using Cardscape.Web.Services;
using Cardscape.Web.Services.Api;

namespace Cardscape.Web.Components.AdminSettings;

/// <summary>
/// Editing session for the admin settings page: keeps the last saved
/// snapshot next to a draft the form binds to. Unsaved changes are detected
/// by record value equality, so there is no per-field dirty tracking.
/// </summary>
public sealed class AdminSettingsEditor(IAdminSettingsApiClient api, InstanceSettingsState instance)
{
    private SystemSettings? _saved;

    public SystemSettings? Draft { get; private set; }

    public bool IsLoaded => Draft is not null;

    /// <summary>True when the draft differs from what the server has (a typed AI key counts).</summary>
    public bool HasChanges =>
        Draft is not null && _saved is not null
        && (!Draft.Equals(_saved) || !string.IsNullOrEmpty(Draft.Ai.ApiKey));

    public Task<string?> LoadAsync() => ApplyAsync(api.GetAsync(), refreshInstance: false);

    public Task<string?> SaveAsync() =>
        Draft is null ? Task.FromResult<string?>(null) : ApplyAsync(api.UpdateAsync(Draft), refreshInstance: true);

    public Task<string?> ResetToDefaultsAsync() => ApplyAsync(api.ResetAsync(), refreshInstance: true);

    /// <summary>Throws away unsaved edits.</summary>
    public void Discard() => Draft = _saved?.DeepCopy();

    /// <summary>Marks the stored AI key for removal on the next save.</summary>
    public void ClearAiApiKey()
    {
        if (Draft is not null)
        {
            Draft.Ai.ApiKey = string.Empty;
        }
    }

    /// <returns>The error message, or null on success.</returns>
    private async Task<string?> ApplyAsync(Task<ApiResult<SystemSettings>> call, bool refreshInstance)
    {
        ApiResult<SystemSettings> result = await call;
        if (result is not { IsSuccess: true, Value: { } settings })
        {
            return result.Error ?? "HTTP error";
        }

        _saved = settings;
        Draft = settings.DeepCopy();
        if (refreshInstance)
        {
            await instance.RefreshAsync();
        }

        return null;
    }
}
