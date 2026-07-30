namespace Cardscape.Application.Integrations.GitHub.DTOs;

/// <summary>Compact projection of a GitHub branch.</summary>
public sealed record GitHubBranchDto(
    string Name,
    string? LastCommitSha,
    bool Protected);

/// <summary>Compact projection of a GitHub pull request.</summary>
public sealed record GitHubPullRequestDto(
    int Number,
    string Title,
    string State,
    string? Url,
    string? HeadRef,
    string? BaseRef,
    DateTimeOffset? CreatedAt);

/// <summary>Compact projection of a GitHub issue.</summary>
public sealed record GitHubIssueDto(
    int Number,
    string Title,
    string State,
    string? Url,
    IReadOnlyList<string> Labels,
    DateTimeOffset? CreatedAt);

/// <summary>Compact projection of a <c>GitHubPullRequestLink</c>.</summary>
public sealed record GitHubPullRequestLinkDto(
    Guid Id,
    Guid CardId,
    string RepoFullName,
    int PullRequestNumber,
    string? PullRequestUrl,
    DateTimeOffset CreatedAt);
