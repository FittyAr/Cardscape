using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Integrations.GitHub;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class GitHubRepoLinkRepository(CardscapeDbContext db)
    : RepositoryBase<GitHubRepoLink, GitHubRepoLinkId>(db), IGitHubRepoLinkRepository
{
    public async Task<IReadOnlyList<GitHubRepoLink>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default)
    {
        IQueryable<GitHubRepoLink> query = Set
            .AsNoTracking()
            .Where(link => link.BoardId == boardId && !link.IsDeleted);
        return await query.ToListOrderedAsync(Db, link => link.CreatedAt, ct: ct);
    }

    public async Task<GitHubRepoLink?> FindForBoardAndRepoAsync(
        BoardId boardId, string repoFullName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(repoFullName))
        {
            return null;
        }

        var needle = repoFullName.Trim().ToLowerInvariant();
        return await Set
            .FirstOrDefaultAsync(link =>
                link.BoardId == boardId
                && !link.IsDeleted
                && link.Active
                && link.RepoFullName == needle, ct);
    }
}

public sealed class GitHubPullRequestLinkRepository(CardscapeDbContext db)
    : RepositoryBase<GitHubPullRequestLink, GitHubPullRequestLinkId>(db), IGitHubPullRequestLinkRepository
{
    public async Task<IReadOnlyList<GitHubPullRequestLink>> ListForCardAsync(
        CardId cardId, CancellationToken ct = default)
    {
        IQueryable<GitHubPullRequestLink> query = Set
            .AsNoTracking()
            .Where(link => link.CardId == cardId && !link.IsDeleted);
        return await query.ToListOrderedAsync(Db, link => link.CreatedAt, ct: ct);
    }
}
