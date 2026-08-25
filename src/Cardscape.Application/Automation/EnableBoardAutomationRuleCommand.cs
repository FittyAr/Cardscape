using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using FluentValidation;
using Wolverine;

namespace Cardscape.Application.Automation;

public sealed record EnableBoardAutomationRuleCommand(Guid RuleId) : IMessage;

public static class EnableBoardAutomationRuleCommandHandler
{
    public static async Task<Result> Handle(
        EnableBoardAutomationRuleCommand command,
        IRepository<BoardAutomationRule, BoardAutomationRuleId> rules,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var rule = await rules.GetByIdAsync(
            new BoardAutomationRuleId(command.RuleId), cancellationToken);
        if (rule is null)
        {
            return Result.Failure(DomainError.NotFound(
                "automation.not_found", "Rule was not found."));
        }

        var board = await boards.GetWithMembersAsync(rule.BoardId, cancellationToken);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        rule.Enable(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}


