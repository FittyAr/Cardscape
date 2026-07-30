using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IEmailIntegrationApiClient
{
    Task<ApiResult<IReadOnlyList<InboundEmailAddressDto>>> ListAddressesAsync(
        Guid workspaceId, CancellationToken ct = default);
    Task<ApiResult<InboundEmailAddressDto>> RegisterAddressAsync(
        Guid workspaceId, string emailAddress, Guid targetListId, string label,
        CancellationToken ct = default);
    Task<ApiResult> UnregisterAddressAsync(
        Guid workspaceId, Guid addressId, CancellationToken ct = default);
}

public sealed class EmailIntegrationApiClient(IHttpClientFactory http) : ApiClientBase(http), IEmailIntegrationApiClient
{
    public async Task<ApiResult<IReadOnlyList<InboundEmailAddressDto>>> ListAddressesAsync(
        Guid workspaceId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/integrations/email/addresses?workspaceId={workspaceId}", ct);
        return await ReadAsync<IReadOnlyList<InboundEmailAddressDto>>(response, ct);
    }

    public async Task<ApiResult<InboundEmailAddressDto>> RegisterAddressAsync(
        Guid workspaceId, string emailAddress, Guid targetListId, string label,
        CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/integrations/email/addresses",
            new RegisterAddressRequest(workspaceId, emailAddress, targetListId, label), ct);
        return await ReadAsync<InboundEmailAddressDto>(response, ct);
    }

    public async Task<ApiResult> UnregisterAddressAsync(
        Guid workspaceId, Guid addressId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync(
            $"api/integrations/email/addresses/{addressId}?workspaceId={workspaceId}", ct);
        return await ReadAsync(response, ct);
    }

    public sealed record RegisterAddressRequest(
        Guid WorkspaceId, string EmailAddress, Guid TargetListId, string Label);
}
