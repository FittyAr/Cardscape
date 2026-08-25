using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using FluentValidation;
using Wolverine;

namespace Cardscape.Application.Automation;

public sealed record CreateBoardAutomationRuleCommand(
    Guid BoardId,
    string Name,
    AutomationTrigger Trigger,
    Guid? TriggerListId,
    AutomationAction Action,
    string? ActionArgument,
    int Position = 0) : IMessage;

public static class CreateBoardAutomationRuleCommandHandler
{
    public static async Task<Result<BoardAutomationRuleDto>> Handle(
        CreateBoardAutomationRuleCommand command,
        IRepository<BoardAutomationRule, BoardAutomationRuleId> rules,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardAutomationRuleDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetWithMembersAsync(
            new BoardId(command.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<BoardAutomationRuleDto>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<BoardAutomationRuleDto>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        var creation = BoardAutomationRule.Create(
            board.Id,
            command.Name,
            command.Trigger,
            command.TriggerListId,
            command.Action,
            command.ActionArgument,
            command.Position,
            clock.UtcNow);
        if (creation.IsFailure)
        {
            return Result.Failure<BoardAutomationRuleDto>(creation.Error);
        }

        await rules.AddAsync(creation.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(BoardAutomationRuleDto.FromEntity(creation.Value));
    }
}


