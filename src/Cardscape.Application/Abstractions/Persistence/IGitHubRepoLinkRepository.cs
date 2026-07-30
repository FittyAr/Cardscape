using Cardscape.Domain.Boards;
using Cardscape.Domain.Integrations.GitHub;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>Read/write repository for <see cref="GitHubRepoLink"/>.</summary>
public interface IGitHubRepoLinkRepository : IRepository<GitHubRepoLink, GitHubRepoLinkId>
{
    /// <summary>Lists every active GitHub repo link registered
    /// against a board. The MCP tools use this to resolve a
    /// board → repo mapping before calling GitHub.</summary>
    Task<IReadOnlyList<GitHubRepoLink>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default);

    /// <summary>Finds the active link for a (board, repo) pair.
    /// Returns <c>null</c> if the board has no link to that
    /// repo.</summary>
    Task<GitHubRepoLink?> FindForBoardAndRepoAsync(
        BoardId boardId, string repoFullName, CancellationToken ct = default);
}

/// <summary>Read/write repository for <see cref="GitHubPullRequestLink"/>.</summary>
public interface IGitHubPullRequestLinkRepository : IRepository<GitHubPullRequestLink, GitHubPullRequestLinkId>
{
    /// <summary>Lists the linked pull requests for a card. The
    /// Web UI shows the link as a badge; the MCP tools can list
    /// it to drive "show me the PR for this card" workflows.</summary>
    Task<IReadOnlyList<GitHubPullRequestLink>> ListForCardAsync(
        Cardscape.Domain.Cards.CardId cardId, CancellationToken ct = default);
}
