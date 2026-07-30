using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface ISlackApiClient
{
    Task<ApiResult<SlackWorkspaceDto?>> GetAsync(Guid workspaceId, CancellationToken ct = default);
    Task<ApiResult<SlackWorkspaceDto>> ConnectAsync(
        Guid workspaceId, string teamId, string teamName, string botToken, CancellationToken ct = default);
    Task<ApiResult<IReadOnlyList<SlackChannelDto>>> ListChannelsAsync(
        Guid workspaceId, Guid boardId, CancellationToken ct = default);
    Task<ApiResult<SlackChannelDto>> LinkChannelAsync(
        Guid workspaceId, Guid slackWorkspaceId, Guid boardId, string channelId,
        string channelName, IReadOnlyList<string> events, CancellationToken ct = default);
    Task<ApiResult> UnlinkChannelAsync(
        Guid workspaceId, Guid channelId, CancellationToken ct = default);
}

public sealed class SlackApiClient(IHttpClientFactory http) : ApiClientBase(http), ISlackApiClient
{
    public async Task<ApiResult<SlackWorkspaceDto?>> GetAsync(Guid workspaceId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/workspaces/{workspaceId}/integrations/slack/", ct);
        return await ReadAsync<SlackWorkspaceDto?>(response, ct);
    }

    public async Task<ApiResult<SlackWorkspaceDto>> ConnectAsync(
        Guid workspaceId, string teamId, string teamName, string botToken, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/integrations/slack/connect",
            new ConnectSlackRequest(workspaceId, teamId, teamName, botToken), ct);
        return await ReadAsync<SlackWorkspaceDto>(response, ct);
    }

    public async Task<ApiResult<IReadOnlyList<SlackChannelDto>>> ListChannelsAsync(
        Guid workspaceId, Guid boardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/workspaces/{workspaceId}/integrations/slack/channels?boardId={boardId}", ct);
        return await ReadAsync<IReadOnlyList<SlackChannelDto>>(response, ct);
    }

    public async Task<ApiResult<SlackChannelDto>> LinkChannelAsync(
        Guid workspaceId, Guid slackWorkspaceId, Guid boardId, string channelId,
        string channelName, IReadOnlyList<string> events, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/integrations/slack/channels",
            new LinkSlackChannelRequest(slackWorkspaceId, boardId, channelId, channelName, events), ct);
        return await ReadAsync<SlackChannelDto>(response, ct);
    }

    public async Task<ApiResult> UnlinkChannelAsync(
        Guid workspaceId, Guid channelId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync(
            $"api/workspaces/{workspaceId}/integrations/slack/channels/{channelId}", ct);
        return await ReadAsync(response, ct);
    }

    public sealed record ConnectSlackRequest(
        Guid WorkspaceId, string TeamId, string TeamName, string BotToken);

    public sealed record LinkSlackChannelRequest(
        Guid SlackWorkspaceId, Guid BoardId, string ChannelId, string ChannelName, IReadOnlyList<string> Events);
}
