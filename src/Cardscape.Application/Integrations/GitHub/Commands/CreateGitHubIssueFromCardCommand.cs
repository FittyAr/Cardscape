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

public sealed record CreateGitHubIssueFromCardCommand(
    Guid CardId, string RepoFullName, string? OverrideTitle, string? OverrideBody) : IMessage;

public static class CreateGitHubIssueFromCardCommandHandler
{
    public static async Task<Result<GitHubIssueDto>> Handle(
        CreateGitHubIssueFromCardCommand command,
        IGitHubService github,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IGitHubRepoLinkRepository repoLinks,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<GitHubIssueDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Card? card = await cards.GetByIdAsync(new CardId(command.CardId), ct);
        if (card is null)
        {
            return Result.Failure<GitHubIssueDto>(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        var list = await lists.GetByIdAsync(card.ListId, ct);
        if (list is null)
        {
            return Result.Failure<GitHubIssueDto>(DomainError.NotFound(
                "lists.not_found", "List was not found."));
        }

        var board = await boards.GetWithMembersAsync(list.BoardId, ct);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<GitHubIssueDto>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        GitHubRepoLink? repoLink = await repoLinks.FindForBoardAndRepoAsync(
            board.Id, command.RepoFullName, ct);
        if (repoLink is null)
        {
            return Result.Failure<GitHubIssueDto>(DomainError.Forbidden(
                "github.repo_not_linked", "This repository is not linked to the board."));
        }

        string title = !string.IsNullOrWhiteSpace(command.OverrideTitle)
            ? command.OverrideTitle
            : card.Title.Value;
        string body = !string.IsNullOrWhiteSpace(command.OverrideBody)
            ? command.OverrideBody
            : card.Description.Value;

        return await github.CreateIssueAsync(command.RepoFullName, title, body, ct);
    }
}
