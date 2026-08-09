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
        var rows = new List<GitHubRepoLink>();
        await foreach (var l in Db.Set<GitHubRepoLink>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (l.BoardId.Value == boardValue && !l.IsDeleted)
            {
                rows.Add(l);
            }
        }
        rows.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
        return rows;
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
        await foreach (var l in Db.Set<GitHubRepoLink>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (l.BoardId.Value == boardValue
                && !l.IsDeleted
                && l.Active
                && string.Equals(l.RepoFullName, needle, StringComparison.Ordinal))
            {
                return l;
            }
        }
        return null;
    }
}

public sealed class GitHubPullRequestLinkRepository(CardscapeDbContext db)
    : RepositoryBase<GitHubPullRequestLink, GitHubPullRequestLinkId>(db), IGitHubPullRequestLinkRepository
{
    public async Task<IReadOnlyList<GitHubPullRequestLink>> ListForCardAsync(
        CardId cardId, CancellationToken ct = default)
    {
        var cardValue = cardId.Value;
        var rows = new List<GitHubPullRequestLink>();
        await foreach (var l in Db.Set<GitHubPullRequestLink>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (l.CardId.Value == cardValue && !l.IsDeleted)
            {
                rows.Add(l);
            }
        }
        rows.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
        return rows;
    }
}
