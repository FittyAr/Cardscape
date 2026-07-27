using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists.Events;
using Cardscape.Domain.Common;
using static Cardscape.Domain.Checklists.Errors.ChecklistErrors;

namespace Cardscape.Domain.Checklists;

/// <summary>A checklist attached to a card.</summary>
public sealed class Checklist : AggregateRoot<ChecklistId>
{
    public CardId CardId { get; private set; } = null!;
    public ChecklistTitle Title { get; private set; } = null!;

    private readonly List<ChecklistItem> _items = [];
    public IReadOnlyCollection<ChecklistItem> Items => _items.AsReadOnly();

    private Checklist() { }

    private Checklist(ChecklistId id, CardId cardId, ChecklistTitle title, Guid createdBy, DateTimeOffset at)
    {
        Id = id;
        CardId = cardId;
        Title = title;
        CreatedBy = createdBy;
        CreatedAt = at;
    }

    public static Result<Checklist> Create(
        ChecklistId id,
        CardId cardId,
        ChecklistTitle title,
        Guid createdBy,
        DateTimeOffset at)
    {
        if (createdBy == Guid.Empty)
        {
            return Result.Failure<Checklist>(DomainError.Validation(
                "checklists.creator_required",
                "Checklist creator is required."));
        }

        var checklist = new Checklist(id, cardId, title, createdBy, at);
        checklist.AddDomainEvent(new ChecklistCreated(id, cardId, title, at));
        return Result.Success(checklist);
    }

    public Result Rename(ChecklistTitle newTitle, DateTimeOffset at)
    {
        if (newTitle.Value == Title.Value)
        {
            return Result.Success();
        }

        Title = newTitle;
        UpdatedAt = at;
        AddDomainEvent(new ChecklistRenamed(Id, newTitle, at));
        return Result.Success();
    }

    public ChecklistItem AddItem(ChecklistItemText text, Position position, DateTimeOffset at)
    {
        var item = ChecklistItem.Create(Id, text, position, at);
        _items.Add(item);
        UpdatedAt = at;
        AddDomainEvent(new ChecklistItemAdded(Id, item.Id, at));
        return item;
    }

    public Result CheckItem(ChecklistItemId itemId, DateTimeOffset at)
    {
        var item = _items.FirstOrDefault(i => i.Id.Value == itemId.Value);
        if (item is null)
        {
            return Result.Failure(Errors.ChecklistErrors.ItemNotFound);
        }

        item.Check(at);
        AddDomainEvent(new ChecklistItemChecked(Id, itemId, at));
        return Result.Success();
    }

    public Result UncheckItem(ChecklistItemId itemId, DateTimeOffset at)
    {
        var item = _items.FirstOrDefault(i => i.Id.Value == itemId.Value);
        if (item is null)
        {
            return Result.Failure(Errors.ChecklistErrors.ItemNotFound);
        }

        item.Uncheck(at);
        AddDomainEvent(new ChecklistItemUnchecked(Id, itemId, at));
        return Result.Success();
    }

    public Result UpdateItem(ChecklistItemId itemId, ChecklistItemText newText, DateTimeOffset at)
    {
        var item = _items.FirstOrDefault(i => i.Id.Value == itemId.Value);
        if (item is null)
        {
            return Result.Failure(Errors.ChecklistErrors.ItemNotFound);
        }

        item.UpdateText(newText, at);
        AddDomainEvent(new ChecklistItemUpdated(Id, itemId, at));
        return Result.Success();
    }

    public Result RemoveItem(ChecklistItemId itemId, DateTimeOffset at)
    {
        var item = _items.FirstOrDefault(i => i.Id.Value == itemId.Value);
        if (item is null)
        {
            return Result.Failure(Errors.ChecklistErrors.ItemNotFound);
        }

        _items.Remove(item);
        AddDomainEvent(new ChecklistItemDeleted(Id, itemId, at));
        return Result.Success();
    }

    public Result Delete(DateTimeOffset at)
    {
        if (IsDeleted)
        {
            return Result.Success();
        }

        IsDeleted = true;
        UpdatedAt = at;
        AddDomainEvent(new ChecklistDeleted(Id, at));
        return Result.Success();
    }
}
