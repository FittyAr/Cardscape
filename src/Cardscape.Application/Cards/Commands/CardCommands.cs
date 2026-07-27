using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Cards.Common;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using MediatR;
using static Cardscape.Domain.Cards.Errors.CardErrors;

namespace Cardscape.Application.Cards.Commands;

public sealed record CreateCardCommand(Guid ListId, string Title, string? Description)
    : IRequest<Result<CardDto>>;

public sealed class CreateCardCommandHandler(
    ICardRepository cards,
    IBoardListRepository lists,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<CreateCardCommand, Result<CardDto>>
{
    public async Task<Result<CardDto>> Handle(
        CreateCardCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var list = await lists.GetByIdAsync(new BoardListId(request.ListId), cancellationToken);
        if (list is null)
        {
            return Result.Failure<CardDto>(DomainError.NotFound(
                "lists.not_found", "List was not found."));
        }

        var titleResult = CardTitle.Create(request.Title);
        if (titleResult.IsFailure)
        {
            return Result.Failure<CardDto>(titleResult.Error);
        }

        var descResult = CardDescription.Create(request.Description);
        if (descResult.IsFailure)
        {
            return Result.Failure<CardDto>(descResult.Error);
        }

        var cardResult = Card.Create(
            CardId.New(),
            new BoardListId(request.ListId),
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

public sealed record RenameCardCommand(Guid CardId, string NewTitle) : IRequest<Result<CardDto>>;

public sealed class RenameCardCommandHandler(
    ICardRepository cards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<RenameCardCommand, Result<CardDto>>
{
    public async Task<Result<CardDto>> Handle(
        RenameCardCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(request.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        var titleResult = CardTitle.Create(request.NewTitle);
        if (titleResult.IsFailure)
        {
            return Result.Failure<CardDto>(titleResult.Error);
        }

        var renameResult = card.Rename(titleResult.Value, clock.UtcNow);
        if (renameResult.IsFailure)
        {
            return Result.Failure<CardDto>(renameResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record ChangeCardDescriptionCommand(Guid CardId, string NewDescription)
    : IRequest<Result<CardDto>>;

public sealed class ChangeCardDescriptionCommandHandler(
    ICardRepository cards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<ChangeCardDescriptionCommand, Result<CardDto>>
{
    public async Task<Result<CardDto>> Handle(
        ChangeCardDescriptionCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(request.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        var descResult = CardDescription.Create(request.NewDescription);
        if (descResult.IsFailure)
        {
            return Result.Failure<CardDto>(descResult.Error);
        }

        var changeResult = card.ChangeDescription(descResult.Value, clock.UtcNow);
        if (changeResult.IsFailure)
        {
            return Result.Failure<CardDto>(changeResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record MoveCardCommand(Guid CardId, Guid NewListId, double NewPosition)
    : IRequest<Result<CardDto>>;

public sealed class MoveCardCommandHandler(
    ICardRepository cards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<MoveCardCommand, Result<CardDto>>
{
    public async Task<Result<CardDto>> Handle(
        MoveCardCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(request.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        var moveResult = card.Move(
            new BoardListId(request.NewListId),
            Position.From(request.NewPosition),
            clock.UtcNow);

        if (moveResult.IsFailure)
        {
            return Result.Failure<CardDto>(moveResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record SetCardDueDateCommand(Guid CardId, DateTimeOffset DueDate)
    : IRequest<Result<CardDto>>;

public sealed class SetCardDueDateCommandHandler(
    ICardRepository cards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<SetCardDueDateCommand, Result<CardDto>>
{
    public async Task<Result<CardDto>> Handle(
        SetCardDueDateCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(request.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        var result = card.SetDueDate(request.DueDate, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record ClearCardDueDateCommand(Guid CardId) : IRequest<Result<CardDto>>;

public sealed class ClearCardDueDateCommandHandler(
    ICardRepository cards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<ClearCardDueDateCommand, Result<CardDto>>
{
    public async Task<Result<CardDto>> Handle(
        ClearCardDueDateCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(request.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        var result = card.ClearDueDate(clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record CompleteCardCommand(Guid CardId) : IRequest<Result<CardDto>>;

public sealed class CompleteCardCommandHandler(
    ICardRepository cards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<CompleteCardCommand, Result<CardDto>>
{
    public async Task<Result<CardDto>> Handle(
        CompleteCardCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(request.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        var result = card.Complete(clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record ReopenCardCommand(Guid CardId) : IRequest<Result<CardDto>>;

public sealed class ReopenCardCommandHandler(
    ICardRepository cards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<ReopenCardCommand, Result<CardDto>>
{
    public async Task<Result<CardDto>> Handle(
        ReopenCardCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(request.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        var result = card.Reopen(clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record ArchiveCardCommand(Guid CardId) : IRequest<Result<CardDto>>;

public sealed class ArchiveCardCommandHandler(
    ICardRepository cards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<ArchiveCardCommand, Result<CardDto>>
{
    public async Task<Result<CardDto>> Handle(
        ArchiveCardCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(request.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        card.Archive(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record RestoreCardCommand(Guid CardId) : IRequest<Result<CardDto>>;

public sealed class RestoreCardCommandHandler(
    ICardRepository cards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<RestoreCardCommand, Result<CardDto>>
{
    public async Task<Result<CardDto>> Handle(
        RestoreCardCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(request.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        card.Restore(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record AssignCardCommand(Guid CardId, Guid UserId) : IRequest<Result<CardDto>>;

public sealed class AssignCardCommandHandler(
    ICardRepository cards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<AssignCardCommand, Result<CardDto>>
{
    public async Task<Result<CardDto>> Handle(
        AssignCardCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(request.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        var result = card.Assign(request.UserId, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record UnassignCardCommand(Guid CardId, Guid UserId) : IRequest<Result<CardDto>>;

public sealed class UnassignCardCommandHandler(
    ICardRepository cards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<UnassignCardCommand, Result<CardDto>>
{
    public async Task<Result<CardDto>> Handle(
        UnassignCardCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(request.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        var result = card.Unassign(request.UserId, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record AttachLabelToCardCommand(Guid CardId, Guid LabelId) : IRequest<Result<CardDto>>;

public sealed class AttachLabelToCardCommandHandler(
    ICardRepository cards,
    ILabelRepository labels,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<AttachLabelToCardCommand, Result<CardDto>>
{
    public async Task<Result<CardDto>> Handle(
        AttachLabelToCardCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(request.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        var label = await labels.GetByIdAsync(new LabelId(request.LabelId), cancellationToken);
        if (label is null)
        {
            return Result.Failure<CardDto>(DomainError.NotFound(
                "labels.not_found", "Label was not found."));
        }

        var link = CardLabel.Create(card.Id, label.Id, clock.UtcNow);
        var result = card.AttachLabel(link, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record DetachLabelFromCardCommand(Guid CardId, Guid LabelId) : IRequest<Result<CardDto>>;

public sealed class DetachLabelFromCardCommandHandler(
    ICardRepository cards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<DetachLabelFromCardCommand, Result<CardDto>>
{
    public async Task<Result<CardDto>> Handle(
        DetachLabelFromCardCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(request.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        var result = card.DetachLabel(new LabelId(request.LabelId), clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public static class CardMappingExtensions
{
    public static CardDto MapToDto(this Card card) => new(
        card.Id.Value,
        card.ListId.Value,
        card.Title.Value,
        card.Description.Value,
        card.Position.Value,
        card.DueDate,
        card.IsArchived,
        card.IsCompleted,
        card.CoverColor?.Value,
        card.CreatedAt,
        card.Members.Count,
        card.CardLabels.Count);
}
