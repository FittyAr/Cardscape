using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Integrations.GitHub.Commands;
using Cardscape.Application.Integrations.GitHub.DTOs;
using Cardscape.Application.Integrations.GoogleDrive.Commands;
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
/// MCP tool surface for the four v1 integrations (Slack, Google
/// Drive, GitHub, inbound email). Every tool is a thin wrapper
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
        RequireAuth();
        var result = await bus.InvokeAsync<Result<SlackWorkspaceDto>>(
            new ConnectSlackWorkspaceCommand(workspaceId, teamId, teamName, botToken), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "integrations_slack_list_channels")]
    public async Task<IReadOnlyList<SlackChannelDto>> SlackListChannels(
        Guid boardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("integrations_slack_list_channels");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<SlackChannelDto>>>(
            new ListSlackChannelsForBoardQuery(boardId), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "integrations_slack_unlink_channel")]
    public async Task<string> SlackUnlinkChannel(Guid channelId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("integrations_slack_unlink_channel");
        RequireAuth();
        var result = await bus.InvokeAsync<Result>(new UnlinkSlackChannelCommand(channelId), ct);
        EnsureUnit(result);
        return "OK";
    }

    // ── Google Drive ────────────────────────────────────────

    [McpServerTool(Name = "integrations_google_drive_picker_url")]
    public async Task<string> GoogleDrivePickerUrl(Guid workspaceId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("integrations_google_drive_picker_url");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<string>>(
            new GetGoogleDrivePickerUrlQuery(workspaceId), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "integrations_google_drive_attach")]
    public async Task<Guid> GoogleDriveAttach(
        Guid cardId, string fileId, string? fileName, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("integrations_google_drive_attach");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<Guid>>(
            new AttachGoogleDriveFileCommand(cardId, fileId, fileName), ct);
        return Ensure(result);
    }

    // ── GitHub ──────────────────────────────────────────────

    [McpServerTool(Name = "integrations_github_list_prs")]
    public async Task<IReadOnlyList<GitHubPullRequestDto>> GitHubListPullRequests(
        Guid boardId, string repoFullName, string state, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("integrations_github_list_prs");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<GitHubPullRequestDto>>>(
            new ListGitHubPullRequestsQuery(boardId, repoFullName, state), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "integrations_github_list_issues")]
    public async Task<IReadOnlyList<GitHubIssueDto>> GitHubListIssues(
        Guid boardId, string repoFullName, string state, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("integrations_github_list_issues");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<GitHubIssueDto>>>(
            new ListGitHubIssuesQuery(boardId, repoFullName, state), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "integrations_github_link_pr")]
    public async Task<GitHubPullRequestLinkDto> GitHubLinkPullRequest(
        Guid cardId, string repoFullName, int pullRequestNumber, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("integrations_github_link_pr");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<GitHubPullRequestLinkDto>>(
            new LinkGitHubPullRequestCommand(cardId, repoFullName, pullRequestNumber), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "integrations_github_create_issue")]
    public async Task<GitHubIssueDto> GitHubCreateIssue(
        Guid cardId, string repoFullName, string? title, string? body, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("integrations_github_create_issue");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<GitHubIssueDto>>(
            new CreateGitHubIssueFromCardCommand(cardId, repoFullName, title, body), ct);
        return Ensure(result);
    }

    // ── Inbound email ───────────────────────────────────────

    [McpServerTool(Name = "integrations_email_list_addresses")]
    public async Task<IReadOnlyList<InboundEmailAddressDto>> EmailListAddresses(
        Guid workspaceId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("integrations_email_list_addresses");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<InboundEmailAddressDto>>>(
            new ListInboundEmailAddressesQuery(workspaceId), ct);
        return Ensure(result);
    }

    // ── helpers ──────────────────────────────────────────────

    private void RequireAuth()
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException(
                "MCP tool call rejected: no authenticated principal. "
                + "Pass a Bearer JWT in the Authorization header on the stdio/stdin transport.");
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
