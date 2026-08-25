using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using FluentValidation;
using Wolverine;

namespace Cardscape.Application.Automation;

public sealed record ListBoardAutomationRulesQuery(Guid BoardId) : IMessage;

public static class ListBoardAutomationRulesQueryHandler
{
    public static async Task<Result<IReadOnlyList<BoardAutomationRuleDto>>> Handle(
        ListBoardAutomationRulesQuery query,
        IBoardRepository boards,
        IAutomationRuleRepository rules,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<BoardAutomationRuleDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetWithMembersAsync(
            new BoardId(query.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<IReadOnlyList<BoardAutomationRuleDto>>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<BoardAutomationRuleDto>>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        var rows = await rules.ListForBoardAsync(
            new BoardId(query.BoardId), cancellationToken);

        return Result.Success<IReadOnlyList<BoardAutomationRuleDto>>(
            rows.Select(BoardAutomationRuleDto.FromEntity).ToList());
    }
}


