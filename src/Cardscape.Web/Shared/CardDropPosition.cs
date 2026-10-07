namespace Cardscape.Web.Shared;

/// <summary>
/// Where a dragged card should land: the destination list plus,
/// optionally, the card the pointer is over and which side of it.
/// A null <see cref="CardId"/> means "end of the list".
/// </summary>
public sealed record CardDropTarget(Guid ListId, Guid? CardId, bool After);

/// <summary>
/// Pure ordering math for kanban drag and drop. Positions are the
/// fractional doubles the Move endpoint takes (see the domain
/// <c>Position</c> value object): inserting between two cards
/// averages their positions, the head halves the first one and the
/// tail adds one to the last.
/// </summary>
public static class CardDropPosition
{
    /// <summary>
    /// Decides which side of <paramref name="hovered"/> the dragged
    /// card goes when the pointer enters it. Within the same list a
    /// card dragged downwards lands after the hovered card and one
    /// dragged upwards lands before it, so every neighbour is
    /// reachable without measuring the element. From another list
    /// the card is inserted before the hovered one.
    /// </summary>
    public static CardDropTarget TargetFor(
        IReadOnlyList<CardSummaryDto> hoveredColumn, CardSummaryDto dragged, CardSummaryDto hovered)
    {
        if (dragged.ListId != hovered.ListId)
        {
            return new CardDropTarget(hovered.ListId, hovered.Id, After: false);
        }

        int draggedIndex = IndexOf(hoveredColumn, dragged.Id);
        int hoveredIndex = IndexOf(hoveredColumn, hovered.Id);
        return new CardDropTarget(hovered.ListId, hovered.Id, After: draggedIndex >= 0 && draggedIndex < hoveredIndex);
    }

    /// <summary>
    /// Position for the dragged card in the destination column, or
    /// null when the drop would leave the order unchanged.
    /// <paramref name="destinationColumn"/> is the column as rendered
    /// (ordered by position; it may still contain the dragged card).
    /// </summary>
    public static double? Compute(
        IReadOnlyList<CardSummaryDto> destinationColumn, Guid draggedCardId, CardDropTarget target)
    {
        int originalIndex = IndexOf(destinationColumn, draggedCardId);
        List<CardSummaryDto> others = destinationColumn.Where(card => card.Id != draggedCardId).ToList();

        int insertIndex = others.Count;
        if (target.CardId is { } hoveredId)
        {
            if (hoveredId == draggedCardId)
            {
                return null;
            }

            int hoveredIndex = IndexOf(others, hoveredId);
            if (hoveredIndex >= 0)
            {
                insertIndex = hoveredIndex + (target.After ? 1 : 0);
            }
        }

        if (originalIndex >= 0 && originalIndex == insertIndex)
        {
            return null;
        }

        return Between(
            insertIndex > 0 ? others[insertIndex - 1].Position : null,
            insertIndex < others.Count ? others[insertIndex].Position : null);
    }

    /// <summary>Fractional position strictly between two neighbours (either may be missing).</summary>
    public static double Between(double? previous, double? next) => (previous, next) switch
    {
        (null, null) => 1.0d,
        (null, { } n) => n > 0 ? n / 2.0d : n - 1.0d,
        ({ } p, null) => p + 1.0d,
        ({ } p, { } n) => (p + n) / 2.0d,
    };

    private static int IndexOf(IReadOnlyList<CardSummaryDto> cards, Guid id)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }
}
