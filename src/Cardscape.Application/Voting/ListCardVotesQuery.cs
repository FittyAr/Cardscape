using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Common;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Voting;

public sealed record ListCardVotesQuery(Guid CardId) : IMessage;

public static class ListCardVotesQueryHandler
{
    public static async Task<Result<CardVoteStateDto>> Handle(
        ListCardVotesQuery query,
        ICardVoteRepository votes,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardVoteStateDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Card? card = await cards.GetByIdAsync(new CardId(query.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardVoteStateDto>(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        var guard = await MembershipGuards.EnsureCanMutateCardAsync(
            card, lists, boards, currentUser.Id.Value, cancellationToken);
        if (guard.IsFailure)
        {
            return Result.Failure<CardVoteStateDto>(guard.Error);
        }

        int count = await votes.CountForCardAsync(card.Id, cancellationToken);
        bool hasVoted = await votes.HasVotedAsync(
            card.Id, currentUser.Id.Value, cancellationToken);

        return Result.Success(new CardVoteStateDto(card.Id.Value, count, hasVoted));
    }
}
