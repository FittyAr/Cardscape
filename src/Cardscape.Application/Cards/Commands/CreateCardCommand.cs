using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Abstractions.Storage;
using Cardscape.Application.Cards.Common;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Common;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Attachments;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Members;
using Cardscape.Domain.Notifications;
using Wolverine;
using static Cardscape.Domain.Cards.Errors.CardErrors;
using Color = Cardscape.Domain.Common.Color;

namespace Cardscape.Application.Cards.Commands;

public sealed record CreateCardCommand(Guid ListId, string Title, string? Description)
    : IMessage;

public static class CreateCardCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        CreateCardCommand command,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICardRepository cards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IActivityRepository activities,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var list = await lists.GetByIdAsync(new BoardListId(command.ListId), cancellationToken);
        if (list is null)
        {
            return Result.Failure<CardDto>(DomainError.NotFound(
                "lists.not_found", "List was not found."));
        }

        var boardGuard = await MembershipGuards.EnsureCanMutateBoardAsync(
            boards, currentUser.Id.Value, list.BoardId.Value, cancellationToken);
        if (boardGuard.IsFailure)
        {
            return Result.Failure<CardDto>(boardGuard.Error);
        }

        var titleResult = CardTitle.Create(command.Title);
        if (titleResult.IsFailure)
        {
            return Result.Failure<CardDto>(titleResult.Error);
        }

        var descResult = CardDescription.Create(command.Description);
        if (descResult.IsFailure)
        {
            return Result.Failure<CardDto>(descResult.Error);
        }

        var cardResult = Card.Create(
            CardId.New(),
            new BoardListId(command.ListId),
            titleResult.Value,
            descResult.Value,
            Position.Start(),
            currentUser.Id.Value,
            clock.UtcNow);

        if (cardResult.IsFailure)
        {
            return Result.Failure<CardDto>(cardResult.Error);
        }

        await cards.AddAsync(cardResult.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var activity = Activity.Create(
            list.BoardId,
            cardResult.Value.Id.Value,
            currentUser.Id.Value,
            ActivityKind.CardCreated,
            $"{{\"title\":\"{cardResult.Value.Title.Value.Replace("\"", "\\\"")}\"}}",
            clock.UtcNow);
        await activities.AddAsync(activity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new CardDto(
            cardResult.Value.Id.Value,
            cardResult.Value.ListId.Value,
            cardResult.Value.Title.Value,
            cardResult.Value.Description.Value,
            cardResult.Value.Position.Value,
            cardResult.Value.DueDate,
            cardResult.Value.IsArchived,
            cardResult.Value.IsCompleted,
            cardResult.Value.CoverColor?.Value,
            cardResult.Value.CreatedAt,
            cardResult.Value.Members.Count,
            cardResult.Value.CardLabels.Count));
    }
}


