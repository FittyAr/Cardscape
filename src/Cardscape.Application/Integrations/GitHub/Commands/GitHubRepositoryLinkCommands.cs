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
