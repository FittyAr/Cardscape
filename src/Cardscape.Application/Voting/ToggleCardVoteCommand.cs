using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Common;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Voting;

public sealed record ToggleCardVoteCommand(Guid CardId) : IMessage;

public static class ToggleCardVoteCommandHandler
{
    public static async Task<Result<CardVoteStateDto>> Handle(
        ToggleCardVoteCommand command,
        ICardVoteRepository votes,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardVoteStateDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Card? card = await cards.GetByIdAsync(new CardId(command.CardId), cancellationToken);
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

        VoteToggleResult toggled = await votes.ToggleAsync(
            card.Id, currentUser.Id.Value, clock.UtcNow, cancellationToken);

        return Result.Success(new CardVoteStateDto(
            card.Id.Value,
            toggled.VoteCount,
            toggled.NowVoted));
    }
}
