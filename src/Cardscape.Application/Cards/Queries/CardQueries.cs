using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Cards.Common;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using MediatR;
using static Cardscape.Domain.Cards.Errors.CardErrors;

namespace Cardscape.Application.Cards.Queries;

public sealed record GetCardQuery(Guid CardId) : IRequest<Result<CardDto>>;

public sealed class GetCardQueryHandler(
    ICardRepository cards,
    ICurrentUser currentUser) : IRequestHandler<GetCardQuery, Result<CardDto>>
{
    public async Task<Result<CardDto>> Handle(
        GetCardQuery request, CancellationToken cancellationToken)
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

        return Result.Success(card.MapToDto());
    }
}

public sealed record ListCardsForBoardQuery(Guid BoardId, bool IncludeArchived = false)
    : IRequest<Result<IReadOnlyList<CardSummaryDto>>>;

public sealed class ListCardsForBoardQueryHandler(
    ICardRepository cards,
    ICurrentUser currentUser) : IRequestHandler<ListCardsForBoardQuery, Result<IReadOnlyList<CardSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<CardSummaryDto>>> Handle(
        ListCardsForBoardQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<CardSummaryDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var items = await cards.ListForBoardAsync(
            new Domain.Boards.BoardId(request.BoardId),
            request.IncludeArchived,
            cancellationToken);

        var rows = items
            .Select(c => new CardSummaryDto(
                c.Id.Value,
                c.ListId.Value,
                c.Title.Value,
                c.Position.Value,
                c.DueDate,
                c.IsCompleted))
            .ToList();

        return Result.Success<IReadOnlyList<CardSummaryDto>>(rows);
    }
}
