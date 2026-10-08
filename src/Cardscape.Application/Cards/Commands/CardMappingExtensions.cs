using Cardscape.Application.Cards.DTOs;
using Cardscape.Domain.Cards;

namespace Cardscape.Application.Cards.Commands;

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
        card.CardLabels.Count,
        LabelIds: card.CardLabels.Select(cardLabel => cardLabel.LabelId.Value).ToList(),
        MemberIds: card.Members.OrderBy(member => member.AssignedAt).Select(member => member.UserId).ToList());

    /// <summary>
    /// Overload that also projects the per-card snooze state.
    /// Used by queries (GetCardQuery, ListCardsForBoardQuery) so
    /// the Web UI can render the "Snoozed" badge without a
    /// second round-trip. When <paramref name="snooze"/> is
    /// <c>null</c> the card is treated as not snoozed.
    /// </summary>
    public static CardDto MapToDto(this Card card, CardSnooze? snooze, DateTimeOffset now) =>
        snooze is null
            ? card.MapToDto()
            : card.MapToDto() with
            {
                IsSnoozed = snooze.IsActive(now),
                SnoozeUntil = snooze.Until
            };

    /// <summary>
    /// BETA-7-#13 — see test-results/BETA-TEST-REPORT.md.
    /// Overload that also projects the mirror source id
    /// (set when the card is a mirror copy of another
    /// card). The Web UI uses the flag to render a
    /// "Mirror" badge so users can tell the two cards with
    /// identical titles apart.
    /// </summary>
    public static CardDto MapToDto(this Card card, CardSnooze? snooze, DateTimeOffset now, Guid? mirrorOfCardId) =>
        (snooze is null
            ? card.MapToDto()
            : card.MapToDto() with
            {
                IsSnoozed = snooze.IsActive(now),
                SnoozeUntil = snooze.Until
            })
        with
        {
            MirrorOfCardId = mirrorOfCardId
        };
}
