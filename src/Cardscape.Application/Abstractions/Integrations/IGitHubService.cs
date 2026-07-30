using Cardscape.Application.Integrations.GitHub.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.GitHub;

namespace Cardscape.Application.Abstractions.Integrations;

/// <summary>
/// Talks to the GitHub REST API. The default implementation is an
/// HTTP client that authenticates with a personal-access token
/// (PAT) from <c>Integrations:GitHub:Token</c>. The interface is
/// the only Application-layer abstraction over GitHub so a fake
/// (used by tests) or a queue-backed implementation can drop in
/// without touching handlers.
/// </summary>
public interface IGitHubService
{
    /// <summary>Lists branches on a repository.</summary>
    Task<Result<IReadOnlyList<GitHubBranchDto>>> ListBranchesAsync(
        string repoFullName, CancellationToken ct = default);

    /// <summary>Lists pull requests on a repository. <paramref name="state"/>
    /// is one of <c>open</c>, <c>closed</c>, or <c>all</c> (the GitHub
    /// default of omitting the filter is also supported).</summary>
    Task<Result<IReadOnlyList<GitHubPullRequestDto>>> ListPullRequestsAsync(
        string repoFullName, string state, CancellationToken ct = default);

    /// <summary>Lists issues on a repository. Pull requests are
    /// included by GitHub's default; the implementation filters
    /// them out so the consumer only sees issues.</summary>
    Task<Result<IReadOnlyList<GitHubIssueDto>>> ListIssuesAsync(
        string repoFullName, string state, CancellationToken ct = default);

    /// <summary>Creates a new issue on the given repo. The
    /// implementation maps a Cardscape card's title → GitHub
    /// issue title and the description → issue body.</summary>
    Task<Result<GitHubIssueDto>> CreateIssueAsync(
        string repoFullName, string title, string body, CancellationToken ct = default);
}
