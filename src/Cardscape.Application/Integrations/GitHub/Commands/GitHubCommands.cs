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

public sealed record LinkGitHubRepoCommand(Guid BoardId, string RepoFullName, IReadOnlyList<string> Events) : IMessage;

public static class LinkGitHubRepoCommandHandler
{
    public static async Task<Result<GitHubRepoLink>> Handle(
        LinkGitHubRepoCommand command,
        IGitHubRepoLinkRepository links,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<GitHubRepoLink>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(command.BoardId), ct);
        if (board is null)
        {
            return Result.Failure<GitHubRepoLink>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<GitHubRepoLink>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        var creation = GitHubRepoLink.Link(
            GitHubRepoLinkId.New(),
            new BoardId(command.BoardId),
            command.RepoFullName,
            command.Events,
            clock.UtcNow);
        if (creation.IsFailure)
        {
            return Result.Failure<GitHubRepoLink>(creation.Error);
        }

        await links.AddAsync(creation.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(creation.Value);
    }
}

public sealed record UnlinkGitHubRepoCommand(Guid LinkId) : IMessage;

public static class UnlinkGitHubRepoCommandHandler
{
    public static async Task<Result> Handle(
        UnlinkGitHubRepoCommand command,
        IGitHubRepoLinkRepository links,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        GitHubRepoLink? link = await links.GetByIdAsync(new GitHubRepoLinkId(command.LinkId), ct);
        if (link is null)
        {
            return Result.Failure(DomainError.NotFound(
                "github.link_not_found", "GitHub repo link was not found."));
        }

        Board? board = await boards.GetWithMembersAsync(link.BoardId, ct);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        link.Deactivate(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}

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

        return await github.ListIssuesAsync(query.RepoFullName, query.State, ct);
    }
}

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

        string title = !string.IsNullOrWhiteSpace(command.OverrideTitle)
            ? command.OverrideTitle
            : card.Title.Value;
        string body = !string.IsNullOrWhiteSpace(command.OverrideBody)
            ? command.OverrideBody
            : card.Description.Value;

        return await github.CreateIssueAsync(command.RepoFullName, title, body, ct);
    }
}
