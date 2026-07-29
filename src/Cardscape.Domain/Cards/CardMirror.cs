using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;

namespace Cardscape.Domain.Cards;

/// <summary>
/// Card mirror: a relationship between a source card and a
/// mirrored copy on another list (or board). The mirrored
/// card is itself a real <see cref="Card"/> row; the
/// relationship row records the linkage so updates can
/// propagate.
///
/// A card can be mirrored to N target lists; the source
/// card's id is the natural key of the "mirror set". The
/// <see cref="MirroredCardId"/> is the id of the card on
/// the target list.
/// </summary>
public sealed class CardMirror : Entity<Guid>
{
    public CardId SourceCardId { get; private set; } = null!;
    public CardId MirroredCardId { get; private set; } = null!;
    public BoardListId TargetListId { get; private set; } = null!;
    public DateTimeOffset MirroredAt { get; private set; }
    public Guid MirroredBy { get; private set; }

    private CardMirror() { }

    private CardMirror(
        Guid id,
        CardId sourceCardId,
        CardId mirroredCardId,
        BoardListId targetListId,
        DateTimeOffset at,
        Guid mirroredBy)
    {
        Id = id;
        SourceCardId = sourceCardId;
        MirroredCardId = mirroredCardId;
        TargetListId = targetListId;
        MirroredAt = at;
        MirroredBy = mirroredBy;
    }

    public static Result<CardMirror> Create(
        CardId sourceCardId,
        CardId mirroredCardId,
        BoardListId targetListId,
        DateTimeOffset at,
        Guid mirroredBy)
    {
        if (sourceCardId == mirroredCardId)
        {
            return Result.Failure<CardMirror>(DomainError.Validation(
                "card_mirror.same_card",
                "A card cannot be mirrored to itself."));
        }
        return Result.Success(new CardMirror(
            Guid.NewGuid(), sourceCardId, mirroredCardId, targetListId, at, mirroredBy));
    }
}
