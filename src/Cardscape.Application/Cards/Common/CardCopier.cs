using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;

namespace Cardscape.Application.Cards.Common;

/// <summary>
/// Shared by "Copy card" and "Copy list": clones a card's title,
/// description, cover, labels, members and checklists (items keep
/// their order and completion state) into a new card. Comments,
/// attachments, votes and activity history stay with the original.
/// The caller saves the unit of work.
/// </summary>
internal static class CardCopier
{
    public static async Task<Result<Card>> CopyAsync(
        Card source,
        BoardListId targetListId,
        CardTitle title,
        Position position,
        Guid copiedBy,
        DateTimeOffset at,
        ICardRepository cards,
        IChecklistRepository checklists,
        CancellationToken ct)
    {
        Result<Card> created = Card.Create(
            CardId.New(), targetListId, title, source.Description, position, copiedBy, at);
        if (created.IsFailure)
        {
            return created;
        }

        Card copy = created.Value;
        copy.SetCoverColor(source.CoverColor, at);
        foreach (CardLabel link in source.CardLabels)
        {
            copy.AttachLabel(CardLabel.Create(copy.Id, link.LabelId, at), at);
        }

        foreach (CardMember member in source.Members.OrderBy(m => m.AssignedAt))
        {
            copy.Assign(member.UserId, at);
        }

        await cards.AddAsync(copy, ct);

        foreach (Checklist checklist in await checklists.ListForCardAsync(source.Id.Value, ct))
        {
            Result<Checklist> clone = Checklist.Create(ChecklistId.New(), copy.Id, checklist.Title, copiedBy, at);
            if (clone.IsFailure)
            {
                return Result.Failure<Card>(clone.Error);
            }

            foreach (ChecklistItem item in checklist.Items.OrderBy(i => i.Position.Value))
            {
                ChecklistItem cloned = clone.Value.AddItem(item.Text, item.Position, at);
                if (item.IsCompleted)
                {
                    clone.Value.CheckItem(cloned.Id, at);
                }
            }

            await checklists.AddAsync(clone.Value, ct);
        }

        return Result.Success(copy);
    }
}
