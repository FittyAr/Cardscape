using Cardscape.Web.Services.Api;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services;

/// <summary>
/// Whether the instance has been through the first-run setup, fetched
/// once from <c>GET /api/setup/status</c> and shared by the router gate
/// in <c>App.razor</c> and the setup wizard. Caching it lets the gate
/// send a fresh install to <c>/setup</c> before any other route renders,
/// and lets the wizard draw its form without a second round-trip.
/// </summary>
public sealed class SetupStatusState(SetupApiClient api)
{
    private SetupStatusDto? _status;
    private Task<SetupStatusDto?>? _pending;

    /// <summary>The cached status, or <c>null</c> when the API could not
    /// be reached; callers then carry on as if the instance were set up
    /// (the server still refuses anything a fresh install cannot do).</summary>
    public Task<SetupStatusDto?> GetAsync() =>
        _status is not null ? Task.FromResult<SetupStatusDto?>(_status) : _pending ??= LoadAsync();

    /// <summary>Forgets the cached value so the next read asks the API.</summary>
    public void Invalidate() => _status = null;

    /// <summary>Records a successful setup without another round-trip.</summary>
    public void MarkInitialized() =>
        _status = new SetupStatusDto(IsInitialized: true, DatabaseProvider: _status?.DatabaseProvider ?? string.Empty);

    private async Task<SetupStatusDto?> LoadAsync()
    {
        try
        {
            ApiResult<SetupStatusDto> result = await api.GetStatusAsync();
            if (result.HasValue)
            {
                _status = result.Value;
            }

            return _status;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        finally
        {
            _pending = null;
        }
    }
}
