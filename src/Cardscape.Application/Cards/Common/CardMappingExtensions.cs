using Cardscape.Application.Cards.DTOs;
using Cardscape.Domain.Cards;

namespace Cardscape.Application.Cards.Common;

/// <summary>Shared extension methods for projecting <see cref="Card"/> aggregates into DTOs.</summary>
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

    /// <summary>
    /// BUG-A5-003 — see test-results/beta/reports/A5-card-extras.md.
    /// Project a card with the three "header counts" the UI
    /// needs (comments / attachments / checklists). The base
    /// <see cref="MapToDto(Card)"/> uses the in-memory aggregate
    /// children, but those are lazy and sometimes empty when
    /// the card is loaded via a slim query — pass the
    /// pre-computed counts from the repository so the UI never
    /// shows 0 just because the children were not eagerly
    /// loaded.
    /// </summary>
    public static CardDto MapToDto(
        this Card card,
        int commentCount,
        int attachmentCount,
        int checklistCount) => new(
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
            card.CardLabels.Count,
            commentCount,
            attachmentCount,
            checklistCount);
}
