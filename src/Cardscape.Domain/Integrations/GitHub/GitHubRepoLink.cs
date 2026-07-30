using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;

namespace Cardscape.Domain.Integrations.GitHub;

/// <summary>
/// A board-to-GitHub-repository link. The repo is identified by
/// <c>owner/name</c> (the GitHub "full name"). <see cref="Events"/>
/// is the canonicalised comma-joined list of event types from
/// <see cref="GitHubEventTypes"/>; the same logic filters which
/// domain events surface in the GitHub integration.
/// </summary>
public sealed class GitHubRepoLink : AggregateRoot<GitHubRepoLinkId>
{
    public BoardId BoardId { get; private set; } = null!;

    /// <summary>GitHub <c>owner/name</c> (e.g. <c>cardscape/cardscape</c>).</summary>
    public string RepoFullName { get; private set; } = string.Empty;

    /// <summary>Comma-joined list of subscribed event types.</summary>
    public string Events { get; private set; } = string.Empty;

    public bool Active { get; private set; } = true;

    // EF Core.
    private GitHubRepoLink() { }

    private GitHubRepoLink(
        GitHubRepoLinkId id,
        BoardId boardId,
        string repoFullName,
        string events,
        DateTimeOffset at)
    {
        Id = id;
        BoardId = boardId;
        RepoFullName = repoFullName;
        Events = events;
        Active = true;
        CreatedAt = at;
    }

    public static Result<GitHubRepoLink> Link(
        GitHubRepoLinkId id,
        BoardId boardId,
        string repoFullName,
        IEnumerable<string> events,
        DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(repoFullName))
        {
            return Result.Failure<GitHubRepoLink>(DomainError.Validation(
                "github.repo_required", "GitHub repo full name is required."));
        }

        if (repoFullName.Length > 200)
        {
            return Result.Failure<GitHubRepoLink>(DomainError.Validation(
                "github.repo_too_long", "GitHub repo full name must be 200 characters or fewer."));
        }

        // GitHub repo names are <owner>/<name> with a single
        // forward slash. Validate the shape so a stray "https…"
        // doesn't slip through.
        if (!repoFullName.Contains('/') || repoFullName.Split('/').Length != 2)
        {
            return Result.Failure<GitHubRepoLink>(DomainError.Validation(
                "github.repo_malformed",
                "GitHub repo full name must look like 'owner/name'."));
        }

        if (events is null)
        {
            return Result.Failure<GitHubRepoLink>(DomainError.Validation(
                "github.events_required", "At least one event type is required."));
        }

        HashSet<string> normalised = new(StringComparer.OrdinalIgnoreCase);
        foreach (string e in events)
        {
            if (string.IsNullOrWhiteSpace(e))
            {
                continue;
            }

            string trimmed = e.Trim().ToLowerInvariant();
            if (!GitHubEventTypes.IsKnown(trimmed))
            {
                return Result.Failure<GitHubRepoLink>(DomainError.Validation(
                    "github.event_unknown",
                    $"Unknown GitHub event type '{e}'. Allowed: "
                    + string.Join(", ", GitHubEventTypes.All)));
            }

            normalised.Add(trimmed);
        }

        if (normalised.Count == 0)
        {
            return Result.Failure<GitHubRepoLink>(DomainError.Validation(
                "github.events_required", "At least one event type is required."));
        }

        return Result.Success(new GitHubRepoLink(
            id, boardId, repoFullName.Trim().ToLowerInvariant(),
            string.Join(",", normalised.OrderBy(s => s, StringComparer.Ordinal)),
            at));
    }

    public bool SubscribesTo(string eventType) =>
        !string.IsNullOrWhiteSpace(eventType)
        && !string.IsNullOrEmpty(Events)
        && Events
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Any(e => string.Equals(e, eventType, StringComparison.OrdinalIgnoreCase));

    public void Deactivate(DateTimeOffset at)
    {
        if (!Active)
        {
            return;
        }

        Active = false;
        UpdatedAt = at;
    }
}

/// <summary>
/// A link from a Cardscape card to a GitHub pull request. The
/// link stores the (repo, PR number) pair so the Web UI can show
/// a badge on the card and the MCP tools can look the link back
/// up without round-tripping to GitHub.
/// </summary>
public sealed class GitHubPullRequestLink : AggregateRoot<GitHubPullRequestLinkId>
{
    public CardId CardId { get; private set; } = null!;
    public string RepoFullName { get; private set; } = string.Empty;
    public int PullRequestNumber { get; private set; }
    public string? PullRequestUrl { get; private set; }

    // EF Core.
    private GitHubPullRequestLink() { }

    private GitHubPullRequestLink(
        GitHubPullRequestLinkId id,
        CardId cardId,
        string repoFullName,
        int pullRequestNumber,
        string? pullRequestUrl,
        DateTimeOffset at)
    {
        Id = id;
        CardId = cardId;
        RepoFullName = repoFullName;
        PullRequestNumber = pullRequestNumber;
        PullRequestUrl = pullRequestUrl;
        CreatedAt = at;
    }

    public static Result<GitHubPullRequestLink> Create(
        CardId cardId,
        string repoFullName,
        int pullRequestNumber,
        string? pullRequestUrl,
        DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(repoFullName))
        {
            return Result.Failure<GitHubPullRequestLink>(DomainError.Validation(
                "github.repo_required", "GitHub repo full name is required."));
        }

        if (pullRequestNumber <= 0)
        {
            return Result.Failure<GitHubPullRequestLink>(DomainError.Validation(
                "github.pr_number_invalid", "GitHub pull request number must be positive."));
        }

        return Result.Success(new GitHubPullRequestLink(
            GitHubPullRequestLinkId.New(),
            cardId, repoFullName.Trim().ToLowerInvariant(),
            pullRequestNumber, pullRequestUrl, at));
    }
}
