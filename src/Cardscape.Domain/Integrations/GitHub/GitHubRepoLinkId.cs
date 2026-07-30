namespace Cardscape.Domain.Integrations.GitHub;

/// <summary>Identifier of a <see cref="GitHubRepoLink"/>.</summary>
public sealed record GitHubRepoLinkId(Guid Value) : Common.GuidId<GitHubRepoLinkId>(Value);

/// <summary>Identifier of a linked <c>GitHubPullRequest</c>.</summary>
public sealed record GitHubPullRequestLinkId(Guid Value) : Common.GuidId<GitHubPullRequestLinkId>(Value);
