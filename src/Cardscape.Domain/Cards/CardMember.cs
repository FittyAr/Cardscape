using Cardscape.Domain.Common;

namespace Cardscape.Domain.Cards;

/// <summary>Join row assigning a user to a card.</summary>
public sealed class CardMember : Entity<CardMemberId>
{
    public CardId CardId { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }

    private CardMember() { }

    private CardMember(CardId cardId, Guid userId, DateTimeOffset at)
    {
        Id = CardMemberId.New();
        CardId = cardId;
        UserId = userId;
        AssignedAt = at;
        CreatedAt = at;
    }

    internal static CardMember Create(CardId cardId, Guid userId, DateTimeOffset at) =>
        new(cardId, userId, at);
}
