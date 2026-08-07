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

public sealed record DisableBoardAutomationRuleCommand(Guid RuleId) : IMessage;

public static class DisableBoardAutomationRuleCommandHandler
{
    public static async Task<Result> Handle(
        DisableBoardAutomationRuleCommand command,
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

        rule.Disable(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

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

public sealed record DeleteBoardAutomationRuleCommand(Guid RuleId) : IMessage;

public static class DeleteBoardAutomationRuleCommandHandler
{
    public static async Task<Result> Handle(
        DeleteBoardAutomationRuleCommand command,
        IRepository<BoardAutomationRule, BoardAutomationRuleId> rules,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
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

        rules.Remove(rule);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

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

public sealed record BoardAutomationRuleDto(
    Guid Id,
    Guid BoardId,
    string Name,
    int Trigger,
    Guid? TriggerListId,
    int Action,
    string? ActionArgument,
    bool IsEnabled,
    int Position)
{
    public static BoardAutomationRuleDto FromEntity(BoardAutomationRule r) => new(
        r.Id.Value,
        r.BoardId.Value,
        r.Name,
        (int)r.Trigger,
        r.TriggerListId,
        (int)r.Action,
        r.ActionArgument,
        r.IsEnabled,
        r.Position);
}
