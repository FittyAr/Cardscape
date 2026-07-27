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
}
