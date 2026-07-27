using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;

namespace Cardscape.Domain.Labels;

/// <summary>Join row attaching a label to a card.</summary>
public sealed class CardLabel : Entity<CardLabelId>
{
    public CardId CardId { get; private set; } = null!;
    public LabelId LabelId { get; private set; } = null!;

    private CardLabel() { }

    private CardLabel(CardId cardId, LabelId labelId, DateTimeOffset at)
    {
        Id = CardLabelId.New();
        CardId = cardId;
        LabelId = labelId;
        CreatedAt = at;
    }

    internal static CardLabel Create(CardId cardId, LabelId labelId, DateTimeOffset at) =>
        new(cardId, labelId, at);
}
