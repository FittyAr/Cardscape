using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Integrations.GitHub.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.GitHub;
using Wolverine;

namespace Cardscape.Application.Integrations.GitHub.Commands;

public sealed record ListGitHubPullRequestsQuery(
    Guid BoardId, string RepoFullName, string State) : IMessage;

public static class ListGitHubPullRequestsQueryHandler
{
    public static async Task<Result<IReadOnlyList<GitHubPullRequestDto>>> Handle(
        ListGitHubPullRequestsQuery query,
        IGitHubService github,
        IGitHubRepoLinkRepository links,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<GitHubPullRequestDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(query.BoardId), ct);
        if (board is null)
        {
            return Result.Failure<IReadOnlyList<GitHubPullRequestDto>>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<GitHubPullRequestDto>>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        GitHubRepoLink? repoLink = await links.FindForBoardAndRepoAsync(
            board.Id, query.RepoFullName, ct);
        if (repoLink is null)
        {
            return Result.Failure<IReadOnlyList<GitHubPullRequestDto>>(DomainError.Forbidden(
                "github.repo_not_linked", "This repository is not linked to the board."));
        }

        return await github.ListPullRequestsAsync(query.RepoFullName, query.State, ct);
    }
}

public sealed record ListGitHubIssuesQuery(
    Guid BoardId, string RepoFullName, string State) : IMessage;

public static class ListGitHubIssuesQueryHandler
{
    public static async Task<Result<IReadOnlyList<GitHubIssueDto>>> Handle(
        ListGitHubIssuesQuery query,
        IGitHubService github,
        IGitHubRepoLinkRepository links,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<GitHubIssueDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(query.BoardId), ct);
        if (board is null)
        {
            return Result.Failure<IReadOnlyList<GitHubIssueDto>>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<GitHubIssueDto>>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        GitHubRepoLink? repoLink = await links.FindForBoardAndRepoAsync(
            board.Id, query.RepoFullName, ct);
        if (repoLink is null)
        {
            return Result.Failure<IReadOnlyList<GitHubIssueDto>>(DomainError.Forbidden(
                "github.repo_not_linked", "This repository is not linked to the board."));
        }

        return await github.ListIssuesAsync(query.RepoFullName, query.State, ct);
    }
}
