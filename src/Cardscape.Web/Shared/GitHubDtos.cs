namespace Cardscape.Web.Shared;

// ── GitHub integration (v1.1.0 §3.9) ───────────────────────
public sealed record GitHubPullRequestDto(
    int Number,
    string Title,
    string State,
    string? Url,
    string? HeadRef,
    string? BaseRef,
    DateTimeOffset? CreatedAt);

public sealed record GitHubIssueDto(
    int Number,
    string Title,
    string State,
    string? Url,
    IReadOnlyList<string> Labels,
    DateTimeOffset? CreatedAt);

public sealed record GitHubPullRequestLinkDto(
    Guid Id,
    Guid CardId,
    string RepoFullName,
    int PullRequestNumber,
    string? PullRequestUrl,
    DateTimeOffset CreatedAt);
