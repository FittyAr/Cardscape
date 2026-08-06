using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IAutomationApiClient
{
    Task<ApiResult<IReadOnlyList<BoardAutomationRuleDto>>> ListRulesAsync(
        Guid boardId, CancellationToken ct = default);

    Task<ApiResult<BoardAutomationRuleDto>> CreateRuleAsync(
        Guid boardId, CreateRuleRequestDto body, CancellationToken ct = default);

    Task<ApiResult> EnableRuleAsync(
        Guid boardId, Guid ruleId, CancellationToken ct = default);

    Task<ApiResult> DisableRuleAsync(
        Guid boardId, Guid ruleId, CancellationToken ct = default);

    Task<ApiResult> DeleteRuleAsync(
        Guid boardId, Guid ruleId, CancellationToken ct = default);
}

public sealed class AutomationApiClient(IHttpClientFactory http)
    : ApiClientBase(http), IAutomationApiClient
{
    public async Task<ApiResult<IReadOnlyList<BoardAutomationRuleDto>>> ListRulesAsync(
        Guid boardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/boards/{boardId}/automation/", ct);
        return await ReadAsync<IReadOnlyList<BoardAutomationRuleDto>>(response, ct);
    }

    public async Task<ApiResult<BoardAutomationRuleDto>> CreateRuleAsync(
        Guid boardId, CreateRuleRequestDto body, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/boards/{boardId}/automation/", body, JsonOptions, ct);
        return await ReadAsync<BoardAutomationRuleDto>(response, ct);
    }

    public async Task<ApiResult> EnableRuleAsync(
        Guid boardId, Guid ruleId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync(
            $"api/boards/{boardId}/automation/{ruleId}/enable", content: null, ct);
        return await ReadAsync(response, ct);
    }

    public async Task<ApiResult> DisableRuleAsync(
        Guid boardId, Guid ruleId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync(
            $"api/boards/{boardId}/automation/{ruleId}/disable", content: null, ct);
        return await ReadAsync(response, ct);
    }

    public async Task<ApiResult> DeleteRuleAsync(
        Guid boardId, Guid ruleId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync(
            $"api/boards/{boardId}/automation/{ruleId}", ct);
        return await ReadAsync(response, ct);
    }
}
