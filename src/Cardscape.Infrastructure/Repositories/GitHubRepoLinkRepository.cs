using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Integrations.GitHub;
using Cardscape.Infrastructure.Persistence;

namespace Cardscape.Infrastructure.Repositories;

public sealed class GitHubRepoLinkRepository(CardscapeDbContext db)
    : RepositoryBase<GitHubRepoLink, GitHubRepoLinkId>(db), IGitHubRepoLinkRepository
{
    public async Task<IReadOnlyList<GitHubRepoLink>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default)
    {
        var boardValue = boardId.Value;
        return await Task.Run<IReadOnlyList<GitHubRepoLink>>(() =>
        {
            return Db.Set<GitHubRepoLink>().AsEnumerable()
                .Where(l => l.BoardId.Value == boardValue && !l.IsDeleted)
                .OrderBy(l => l.CreatedAt)
                .ToList();
        }, ct);
    }

    public async Task<GitHubRepoLink?> FindForBoardAndRepoAsync(
        BoardId boardId, string repoFullName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(repoFullName))
        {
            return null;
        }

        var boardValue = boardId.Value;
        var needle = repoFullName.Trim().ToLowerInvariant();
        return await Task.Run<GitHubRepoLink?>(() =>
        {
            return Db.Set<GitHubRepoLink>().AsEnumerable()
                .Where(l => l.BoardId.Value == boardValue
                            && !l.IsDeleted
                            && l.Active
                            && string.Equals(l.RepoFullName, needle, StringComparison.Ordinal))
                .FirstOrDefault();
        }, ct);
    }
}

public sealed class GitHubPullRequestLinkRepository(CardscapeDbContext db)
    : RepositoryBase<GitHubPullRequestLink, GitHubPullRequestLinkId>(db), IGitHubPullRequestLinkRepository
{
    public async Task<IReadOnlyList<GitHubPullRequestLink>> ListForCardAsync(
        CardId cardId, CancellationToken ct = default)
    {
        var cardValue = cardId.Value;
        return await Task.Run<IReadOnlyList<GitHubPullRequestLink>>(() =>
        {
            return Db.Set<GitHubPullRequestLink>().AsEnumerable()
                .Where(l => l.CardId.Value == cardValue && !l.IsDeleted)
                .OrderBy(l => l.CreatedAt)
                .ToList();
        }, ct);
    }
}
