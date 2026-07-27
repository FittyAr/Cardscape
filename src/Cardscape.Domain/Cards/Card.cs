using Cardscape.Domain.Cards.Errors;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using static Cardscape.Domain.Cards.Errors.CardErrors;

namespace Cardscape.Domain.Cards;

/// <summary>
/// A card is the atomic unit of work. A card lives in exactly one
/// list and can be moved to another list. Cards own members,
/// labels (via join rows), and a due date.
/// </summary>
public sealed class Card : AggregateRoot<CardId>
{
    public BoardListId ListId { get; private set; } = null!;
    public CardTitle Title { get; private set; } = null!;
    public CardDescription Description { get; private set; } = null!;
    public Position Position { get; private set; }
    public DateTimeOffset? DueDate { get; private set; }
    public bool IsArchived { get; private set; }
    public bool IsCompleted { get; private set; }
    public Color? CoverColor { get; private set; }

    private readonly List<CardMember> _members = [];
    public IReadOnlyCollection<CardMember> Members => _members.AsReadOnly();

    private readonly List<CardLabel> _cardLabels = [];
    public IReadOnlyCollection<CardLabel> CardLabels => _cardLabels.AsReadOnly();

    private Card() { }

    private Card(
        CardId id,
        BoardListId listId,
        CardTitle title,
        CardDescription description,
        Position position,
        Guid createdBy,
        DateTimeOffset at)
    {
        Id = id;
        ListId = listId;
        Title = title;
        Description = description;
        Position = position;
        CreatedBy = createdBy;
        CreatedAt = at;
    }

    public static Result<Card> Create(
        CardId id,
        BoardListId listId,
        CardTitle title,
        CardDescription description,
        Position position,
        Guid createdBy,
        DateTimeOffset at)
    {
        if (createdBy == Guid.Empty)
        {
            return Result.Failure<Card>(DomainError.Validation(
                "cards.creator_required",
                "Card creator is required."));
        }

        var card = new Card(id, listId, title, description, position, createdBy, at);
        card.AddDomainEvent(new CardCreated(id, listId, title, at));
        return Result.Success(card);
    }

    public Result Rename(CardTitle newTitle, DateTimeOffset at)
    {
        if (IsArchived)
        {
            return Result.Failure(CardErrors.Archived);
        }

        if (newTitle.Value == Title.Value)
        {
            return Result.Success();
        }

        Title = newTitle;
        UpdatedAt = at;
        AddDomainEvent(new CardRenamed(Id, newTitle, at));
        return Result.Success();
    }

    public Result ChangeDescription(CardDescription newDescription, DateTimeOffset at)
    {
        if (IsArchived)
        {
            return Result.Failure(CardErrors.Archived);
        }

        if (newDescription.Value == Description.Value)
        {
            return Result.Success();
        }

        Description = newDescription;
        UpdatedAt = at;
        AddDomainEvent(new CardDescriptionChanged(Id, newDescription, at));
        return Result.Success();
    }

    public Result Move(BoardListId newListId, Position newPosition, DateTimeOffset at)
    {
        if (IsArchived)
        {
            return Result.Failure(CardErrors.Archived);
        }

        var sameList = newListId.Value == ListId.Value;
        var samePosition = Math.Abs(newPosition.Value - Position.Value) < double.Epsilon;
        if (sameList && samePosition)
        {
            return Result.Success();
        }

        ListId = newListId;
        Position = newPosition;
        UpdatedAt = at;
        AddDomainEvent(new CardMoved(Id, newListId, newPosition, at));
        return Result.Success();
    }

    public Result SetDueDate(DateTimeOffset dueDate, DateTimeOffset at)
    {
        if (IsArchived)
        {
            return Result.Failure(CardErrors.Archived);
        }

        if (DueDate.HasValue && DueDate.Value == dueDate)
        {
            return Result.Success();
        }

        DueDate = dueDate;
        UpdatedAt = at;
        AddDomainEvent(new CardDueDateSet(Id, dueDate, at));
        return Result.Success();
    }

    public Result ClearDueDate(DateTimeOffset at)
    {
        if (IsArchived)
        {
            return Result.Failure(CardErrors.Archived);
        }

        if (!DueDate.HasValue)
        {
            return Result.Success();
        }

        DueDate = null;
        UpdatedAt = at;
        AddDomainEvent(new CardDueDateCleared(Id, at));
        return Result.Success();
    }

    public Result Complete(DateTimeOffset at)
    {
        if (IsCompleted)
        {
            return Result.Success();
        }

        IsCompleted = true;
        UpdatedAt = at;
        AddDomainEvent(new CardCompleted(Id, at));
        return Result.Success();
    }

    public Result Reopen(DateTimeOffset at)
    {
        if (!IsCompleted)
        {
            return Result.Success();
        }

        IsCompleted = false;
        UpdatedAt = at;
        AddDomainEvent(new CardReopened(Id, at));
        return Result.Success();
    }

    public Result SetCoverColor(Color? color, DateTimeOffset at)
    {
        if (IsArchived)
        {
            return Result.Failure(CardErrors.Archived);
        }

        CoverColor = color;
        UpdatedAt = at;
        return Result.Success();
    }

    public void Archive(DateTimeOffset at)
    {
        if (IsArchived)
        {
            return;
        }

        IsArchived = true;
        UpdatedAt = at;
        AddDomainEvent(new CardArchived(Id, at));
    }

    public void Restore(DateTimeOffset at)
    {
        if (!IsArchived)
        {
            return;
        }

        IsArchived = false;
        UpdatedAt = at;
        AddDomainEvent(new CardRestored(Id, at));
    }

    public Result Assign(Guid userId, DateTimeOffset at)
    {
        if (IsArchived)
        {
            return Result.Failure(CardErrors.Archived);
        }

        if (_members.Any(m => m.UserId == userId))
        {
            return Result.Failure(CardErrors.AlreadyAssigned);
        }

        _members.Add(CardMember.Create(Id, userId, at));
        UpdatedAt = at;
        return Result.Success();
    }

    public Result Unassign(Guid userId, DateTimeOffset at)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member is null)
        {
            return Result.Failure(CardErrors.NotAssigned);
        }

        _members.Remove(member);
        UpdatedAt = at;
        return Result.Success();
    }

    public Result AttachLabel(CardLabel cardLabel, DateTimeOffset at)
    {
        if (IsArchived)
        {
            return Result.Failure(CardErrors.Archived);
        }

        if (_cardLabels.Any(cl => cl.LabelId.Value == cardLabel.LabelId.Value))
        {
            return Result.Success();
        }

        _cardLabels.Add(cardLabel);
        UpdatedAt = at;
        return Result.Success();
    }

    public Result DetachLabel(LabelId labelId, DateTimeOffset at)
    {
        if (IsArchived)
        {
            return Result.Failure(CardErrors.Archived);
        }

        var link = _cardLabels.FirstOrDefault(cl => cl.LabelId.Value == labelId.Value);
        if (link is null)
        {
            return Result.Success();
        }

        _cardLabels.Remove(link);
        UpdatedAt = at;
        return Result.Success();
    }
}
