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

public sealed record LinkGitHubPullRequestCommand(
    Guid CardId, string RepoFullName, int PullRequestNumber) : IMessage;

public static class LinkGitHubPullRequestCommandHandler
{
    public static async Task<Result<GitHubPullRequestLinkDto>> Handle(
        LinkGitHubPullRequestCommand command,
        IGitHubPullRequestLinkRepository links,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IGitHubRepoLinkRepository repoLinks,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<GitHubPullRequestLinkDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Card? card = await cards.GetByIdAsync(new CardId(command.CardId), ct);
        if (card is null)
        {
            return Result.Failure<GitHubPullRequestLinkDto>(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        var list = await lists.GetByIdAsync(card.ListId, ct);
        if (list is null)
        {
            return Result.Failure<GitHubPullRequestLinkDto>(DomainError.NotFound(
                "lists.not_found", "List was not found."));
        }

        var board = await boards.GetWithMembersAsync(list.BoardId, ct);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<GitHubPullRequestLinkDto>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        GitHubRepoLink? repoLink = await repoLinks.FindForBoardAndRepoAsync(
            board.Id, command.RepoFullName, ct);
        if (repoLink is null)
        {
            return Result.Failure<GitHubPullRequestLinkDto>(DomainError.Forbidden(
                "github.repo_not_linked", "This repository is not linked to the board."));
        }

        var creation = GitHubPullRequestLink.Create(
            new CardId(command.CardId),
            command.RepoFullName,
            command.PullRequestNumber,
            $"https://github.com/{command.RepoFullName}/pull/{command.PullRequestNumber}",
            clock.UtcNow);
        if (creation.IsFailure)
        {
            return Result.Failure<GitHubPullRequestLinkDto>(creation.Error);
        }

        await links.AddAsync(creation.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new GitHubPullRequestLinkDto(
            creation.Value.Id.Value,
            creation.Value.CardId.Value,
            creation.Value.RepoFullName,
            creation.Value.PullRequestNumber,
            creation.Value.PullRequestUrl,
            creation.Value.CreatedAt));
    }
}
