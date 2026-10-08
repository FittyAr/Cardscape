using Cardscape.Contracts.Settings;

namespace Cardscape.Web.Services.Api;

/// <summary>Client for <c>/api/admin/settings</c> (administrators only).</summary>
public interface IAdminSettingsApiClient
{
    Task<ApiResult<SystemSettings>> GetAsync(CancellationToken ct = default);

    Task<ApiResult<SystemSettings>> UpdateAsync(SystemSettings settings, CancellationToken ct = default);

    Task<ApiResult<SystemSettings>> ResetAsync(CancellationToken ct = default);

    Task<ApiResult<IReadOnlyList<RuntimeConfigurationEntry>>> GetRuntimeConfigurationAsync(CancellationToken ct = default);

    Task<ApiResult<SystemDiagnostics>> GetDiagnosticsAsync(CancellationToken ct = default);

    Task<ApiResult<AiConnectionTestResult>> TestAiAsync(CancellationToken ct = default);

    /// <summary>Emails the signed-in administrator using the saved SMTP settings.</summary>
    Task<ApiResult<EmailTestResult>> TestEmailAsync(string language, CancellationToken ct = default);
}

public sealed class AdminSettingsApiClient(IHttpClientFactory httpClientFactory)
    : ApiClientBase(httpClientFactory), IAdminSettingsApiClient
{
    private const string Route = "api/admin/settings";

    public async Task<ApiResult<SystemSettings>> GetAsync(CancellationToken ct = default) =>
        await ReadAsync<SystemSettings>(await CreateClient().GetAsync(Route, ct), ct);

    public async Task<ApiResult<SystemSettings>> UpdateAsync(SystemSettings settings, CancellationToken ct = default) =>
        await ReadAsync<SystemSettings>(await CreateClient().PutAsJsonAsync(Route, settings, JsonOptions, ct), ct);

    public async Task<ApiResult<SystemSettings>> ResetAsync(CancellationToken ct = default) =>
        await ReadAsync<SystemSettings>(await CreateClient().PostAsync($"{Route}/reset", null, ct), ct);

    public async Task<ApiResult<IReadOnlyList<RuntimeConfigurationEntry>>> GetRuntimeConfigurationAsync(CancellationToken ct = default) =>
        await ReadAsync<IReadOnlyList<RuntimeConfigurationEntry>>(await CreateClient().GetAsync($"{Route}/runtime", ct), ct);

    public async Task<ApiResult<SystemDiagnostics>> GetDiagnosticsAsync(CancellationToken ct = default) =>
        await ReadAsync<SystemDiagnostics>(await CreateClient().GetAsync($"{Route}/diagnostics", ct), ct);

    public async Task<ApiResult<AiConnectionTestResult>> TestAiAsync(CancellationToken ct = default) =>
        await ReadAsync<AiConnectionTestResult>(await CreateClient().PostAsync($"{Route}/test-ai", null, ct), ct);

    public async Task<ApiResult<EmailTestResult>> TestEmailAsync(string language, CancellationToken ct = default) =>
        await ReadAsync<EmailTestResult>(
            await CreateClient().PostAsync($"{Route}/test-email?language={Uri.EscapeDataString(language)}", null, ct), ct);
}
