using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface INotificationsApiClient
{
    Task<ApiResult<IReadOnlyList<NotificationDto>>> ListAsync(
        bool unreadOnly = false, int skip = 0, int take = 50, CancellationToken ct = default);

    Task<ApiResult<int>> GetUnreadCountAsync(CancellationToken ct = default);

    Task<ApiResult> MarkReadAsync(Guid notificationId, CancellationToken ct = default);

    Task<ApiResult> MarkAllReadAsync(CancellationToken ct = default);
}

public sealed class NotificationsApiClient(IHttpClientFactory http)
    : ApiClientBase(http), INotificationsApiClient
{
    public async Task<ApiResult<IReadOnlyList<NotificationDto>>> ListAsync(
        bool unreadOnly = false, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var url = $"api/notifications/?unreadOnly={(unreadOnly ? "true" : "false")}&skip={skip}&take={take}";
        HttpResponseMessage response = await CreateClient().GetAsync(url, ct);
        return await ReadAsync<IReadOnlyList<NotificationDto>>(response, ct);
    }

    public async Task<ApiResult<int>> GetUnreadCountAsync(CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync("api/notifications/unread-count", ct);
        return await ReadAsync<int>(response, ct);
    }

    public async Task<ApiResult> MarkReadAsync(Guid notificationId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync(
            $"api/notifications/{notificationId}/read", content: null, ct);
        return await ReadAsync(response, ct);
    }

    public async Task<ApiResult> MarkAllReadAsync(CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync(
            "api/notifications/mark-all-read", content: null, ct);
        return await ReadAsync(response, ct);
    }
}
