using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Domain.Integrations.Slack;

/// <summary>
/// Per-workspace Slack connection. <see cref="ProtectedBotToken"/>
/// contains Data Protection ciphertext of the bot OAuth token.
/// One workspace maps to exactly
/// one Slack team; the
/// <see cref="SlackChannel"/> aggregate maps a board inside the
/// workspace to a specific channel on that team.
/// </summary>
public sealed class SlackWorkspace : AggregateRoot<SlackWorkspaceId>
{
    public WorkspaceId WorkspaceId { get; private set; } = null!;

    /// <summary>Slack team id (<c>T…</c>) the bot is installed in.</summary>
    public string TeamId { get; private set; } = string.Empty;

    /// <summary>Human-readable Slack team / workspace name.</summary>
    public string TeamName { get; private set; } = string.Empty;

    /// <summary>Data Protection ciphertext of the bot token.</summary>
    public string ProtectedBotToken { get; private set; } = string.Empty;

    /// <summary>UTC timestamp of the last successful API call,
    /// or <c>null</c> if no call has succeeded yet.</summary>
    public DateTimeOffset? LastUsedAt { get; private set; }

    public bool Active { get; private set; } = true;

    // EF Core.
    private SlackWorkspace() { }

    private SlackWorkspace(
        SlackWorkspaceId id,
        WorkspaceId workspaceId,
        string teamId,
        string teamName,
        string protectedBotToken,
        DateTimeOffset at)
    {
        Id = id;
        WorkspaceId = workspaceId;
        TeamId = teamId;
        TeamName = teamName;
        ProtectedBotToken = protectedBotToken;
        Active = true;
        CreatedAt = at;
    }

    public static Result<SlackWorkspace> Connect(
        SlackWorkspaceId id,
        WorkspaceId workspaceId,
        string teamId,
        string teamName,
        string protectedBotToken,
        DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(teamId))
        {
            return Result.Failure<SlackWorkspace>(DomainError.Validation(
                "slack.team_id_required", "Slack team id is required."));
        }

        if (teamId.Length > 32)
        {
            return Result.Failure<SlackWorkspace>(DomainError.Validation(
                "slack.team_id_too_long", "Slack team id must be 32 characters or fewer."));
        }

        if (string.IsNullOrWhiteSpace(teamName))
        {
            return Result.Failure<SlackWorkspace>(DomainError.Validation(
                "slack.team_name_required", "Slack team name is required."));
        }

        if (teamName.Length > 200)
        {
            return Result.Failure<SlackWorkspace>(DomainError.Validation(
                "slack.team_name_too_long", "Slack team name must be 200 characters or fewer."));
        }

        if (string.IsNullOrWhiteSpace(protectedBotToken) || protectedBotToken.Length > 2048)
        {
            return Result.Failure<SlackWorkspace>(DomainError.Validation(
                "slack.bot_token_protected_invalid",
                "Protected Slack bot token is invalid."));
        }

        return Result.Success(new SlackWorkspace(
            id, workspaceId, teamId.Trim(), teamName.Trim(),
            protectedBotToken, at));
    }

    /// <summary>Records a successful outbound call. Idempotent.</summary>
    public void RecordUse(DateTimeOffset at)
    {
        if (!Active)
        {
            return;
        }

        LastUsedAt = at;
        UpdatedAt = at;
    }

    /// <summary>Disables the workspace connection. The channel
    /// mappings stay in the table so the audit trail is preserved.</summary>
    public void Deactivate(DateTimeOffset at)
    {
        if (!Active)
        {
            return;
        }

        Active = false;
        UpdatedAt = at;
    }

    /// <summary>Re-enables the workspace connection. Idempotent.</summary>
    public void Activate(DateTimeOffset at)
    {
        if (Active)
        {
            return;
        }

        Active = true;
        UpdatedAt = at;
    }

    /// <summary>Replaces the Slack installation identity and token after a fresh OAuth grant.</summary>
    public Result Reconnect(
        string teamId,
        string teamName,
        string protectedBotToken,
        DateTimeOffset at)
    {
        Result<SlackWorkspace> candidate = Connect(
            Id, WorkspaceId, teamId, teamName, protectedBotToken, at);
        if (candidate.IsFailure)
        {
            return Result.Failure(candidate.Error);
        }

        TeamId = candidate.Value.TeamId;
        TeamName = candidate.Value.TeamName;
        ProtectedBotToken = candidate.Value.ProtectedBotToken;
        Active = true;
        UpdatedAt = at;
        return Result.Success();
    }
}
