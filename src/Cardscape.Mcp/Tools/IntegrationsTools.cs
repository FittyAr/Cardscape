using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Integrations.GitHub.Commands;
using Cardscape.Application.Integrations.GitHub.DTOs;
using Cardscape.Application.Integrations.InboundEmail.DTOs;
using Cardscape.Application.Integrations.InboundEmail.Queries;
using Cardscape.Application.Integrations.Slack.Commands;
using Cardscape.Application.Integrations.Slack.DTOs;
using Cardscape.Application.Integrations.Slack.Queries;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Observability;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

/// <summary>
/// MCP tool surface for Slack, GitHub, and inbound email. Every tool is a thin wrapper
/// around the same Application-layer command / query the REST
/// API uses, so authorization, validation, and side effects
/// (audit, realtime push) all stay in one place.
/// </summary>
[McpServerToolType]
public sealed class IntegrationsTools(IMessageBus bus, ICurrentUser currentUser)
{
    // ── Slack ───────────────────────────────────────────────

    [McpServerTool(Name = "integrations_slack_connect")]
    public async Task<SlackWorkspaceDto> SlackConnect(
        Guid workspaceId, string teamId, string teamName, string botToken, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("integrations_slack_connect");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<SlackWorkspaceDto>>(
                new ConnectSlackWorkspaceCommand(workspaceId, teamId, teamName, botToken), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "integrations_slack_list_channels")]
    public async Task<IReadOnlyList<SlackChannelDto>> SlackListChannels(
        Guid workspaceId, Guid boardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("integrations_slack_list_channels");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<IReadOnlyList<SlackChannelDto>>>(
                new ListSlackChannelsForBoardQuery(workspaceId, boardId), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "integrations_slack_unlink_channel")]
    public async Task<string> SlackUnlinkChannel(Guid workspaceId, Guid channelId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("integrations_slack_unlink_channel");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result>(
                new UnlinkSlackChannelCommand(workspaceId, channelId), ct);
            EnsureUnit(result);
            __mcpSpan.MarkSuccess();
            return "OK";
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    // ── GitHub ──────────────────────────────────────────────

    [McpServerTool(Name = "integrations_github_list_prs")]
    public async Task<IReadOnlyList<GitHubPullRequestDto>> GitHubListPullRequests(
        Guid boardId, string repoFullName, string state, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("integrations_github_list_prs");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<IReadOnlyList<GitHubPullRequestDto>>>(
                new ListGitHubPullRequestsQuery(boardId, repoFullName, state), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "integrations_github_list_issues")]
    public async Task<IReadOnlyList<GitHubIssueDto>> GitHubListIssues(
        Guid boardId, string repoFullName, string state, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("integrations_github_list_issues");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<IReadOnlyList<GitHubIssueDto>>>(
                new ListGitHubIssuesQuery(boardId, repoFullName, state), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "integrations_github_link_pr")]
    public async Task<GitHubPullRequestLinkDto> GitHubLinkPullRequest(
        Guid cardId, string repoFullName, int pullRequestNumber, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("integrations_github_link_pr");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<GitHubPullRequestLinkDto>>(
                new LinkGitHubPullRequestCommand(cardId, repoFullName, pullRequestNumber), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "integrations_github_create_issue")]
    public async Task<GitHubIssueDto> GitHubCreateIssue(
        Guid cardId, string repoFullName, string? title, string? body, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("integrations_github_create_issue");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<GitHubIssueDto>>(
                new CreateGitHubIssueFromCardCommand(cardId, repoFullName, title, body), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    // ── Inbound email ───────────────────────────────────────

    [McpServerTool(Name = "integrations_email_list_addresses")]
    public async Task<IReadOnlyList<InboundEmailAddressDto>> EmailListAddresses(
        Guid workspaceId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("integrations_email_list_addresses");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<IReadOnlyList<InboundEmailAddressDto>>>(
                new ListInboundEmailAddressesQuery(workspaceId), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    // ── helpers ──────────────────────────────────────────────

    private void RequireAuth()
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException(
                "MCP tool call rejected: no authenticated principal. "
                + "Pass the API token as an Authorization: Bearer header to the MCP HTTP endpoint.");
        }
    }

    private static T Ensure<T>(Result<T> result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"{result.Error.Code}: {result.Error.Message}");
        }

        return result.Value!;
    }

    private static void EnsureUnit(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"{result.Error.Code}: {result.Error.Message}");
        }
    }
}
