using Cardscape.Domain.Cards;
using Cardscape.Domain.Cards.Errors;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;

namespace Cardscape.UnitTests.Domain.Aggregates;

public class CardTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.UtcNow;

    private static Card NewCard(Guid? creatorId = null) =>
        Card.Create(
            CardId.New(),
            BoardListId.New(),
            CardTitle.Create("My Card").Value,
            CardDescription.Create("desc").Value,
            Position.Start(),
            creatorId ?? Guid.NewGuid(),
            At).Value;

    [Fact]
    public void Create_WithValidData_PopulatesFields()
    {
        var card = NewCard();

        card.Title.Value.Should().Be("My Card");
        card.Position.Value.Should().Be(Position.Start().Value);
        card.IsArchived.Should().BeFalse();
        card.IsCompleted.Should().BeFalse();
        card.Members.Should().BeEmpty();
        card.CardLabels.Should().BeEmpty();
    }

    [Fact]
    public void Create_RaisesCardCreatedEvent()
    {
        var card = NewCard();

        card.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CardCreated>();
    }

    [Fact]
    public void Rename_WithDifferentTitle_UpdatesAndRaisesEvent()
    {
        var card = NewCard();
        card.ClearDomainEvents();
        var newTitle = CardTitle.Create("New Title").Value;

        var result = card.Rename(newTitle, At);

        result.IsSuccess.Should().BeTrue();
        card.Title.Value.Should().Be("New Title");
        card.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CardRenamed>();
    }

    [Fact]
    public void Rename_WithSameTitle_IsNoop()
    {
        var card = NewCard();
        card.ClearDomainEvents();

        var result = card.Rename(CardTitle.Create("My Card").Value, At);

        result.IsSuccess.Should().BeTrue();
        card.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Rename_WhenArchived_ReturnsArchivedFailure()
    {
        var card = NewCard();
        card.Archive(At);

        var result = card.Rename(CardTitle.Create("New").Value, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CardErrors.Archived.Code);
    }

    [Fact]
    public void Move_ToDifferentList_UpdatesListAndRaisesEvent()
    {
        var card = NewCard();
        card.ClearDomainEvents();
        var newList = BoardListId.New();
        var newPos = Position.From(2.0);

        var result = card.Move(newList, newPos, At);

        result.IsSuccess.Should().BeTrue();
        card.ListId.Should().Be(newList);
        card.Position.Value.Should().Be(2.0);
        card.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CardMoved>();
    }

    [Fact]
    public void Move_ToSameListAndPosition_IsNoop()
    {
        var card = NewCard();
        card.ClearDomainEvents();
        var sameList = card.ListId;
        var samePos = card.Position;

        var result = card.Move(sameList, samePos, At);

        result.IsSuccess.Should().BeTrue();
        card.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Move_WhenArchived_ReturnsArchivedFailure()
    {
        var card = NewCard();
        card.Archive(At);

        var result = card.Move(BoardListId.New(), Position.From(2.0), At);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CardErrors.Archived.Code);
    }

    [Fact]
    public void SetDueDate_WithNewDate_RaisesEvent()
    {
        var card = NewCard();
        card.ClearDomainEvents();
        var due = At.AddDays(7);

        var result = card.SetDueDate(due, At);

        result.IsSuccess.Should().BeTrue();
        card.DueDate.Should().Be(due);
        card.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CardDueDateSet>();
    }

    [Fact]
    public void SetDueDate_WhenAlreadySetToSameValue_IsNoop()
    {
        var card = NewCard();
        var due = At.AddDays(7);
        card.SetDueDate(due, At);
        card.ClearDomainEvents();

        var result = card.SetDueDate(due, At);

        result.IsSuccess.Should().BeTrue();
        card.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ClearDueDate_AfterSet_RaisesClearedEvent()
    {
        var card = NewCard();
        card.SetDueDate(At.AddDays(7), At);
        card.ClearDomainEvents();

        var result = card.ClearDueDate(At);

        result.IsSuccess.Should().BeTrue();
        card.DueDate.Should().BeNull();
        card.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CardDueDateCleared>();
    }

    [Fact]
    public void Complete_FromIncomplete_RaisesCompletedEvent()
    {
        var card = NewCard();
        card.ClearDomainEvents();

        var result = card.Complete(At);

        result.IsSuccess.Should().BeTrue();
        card.IsCompleted.Should().BeTrue();
        card.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CardCompleted>();
    }

    [Fact]
    public void Complete_WhenAlreadyCompleted_IsNoop()
    {
        var card = NewCard();
        card.Complete(At);
        card.ClearDomainEvents();

        var result = card.Complete(At);

        result.IsSuccess.Should().BeTrue();
        card.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Reopen_AfterComplete_RaisesReopenedEvent()
    {
        var card = NewCard();
        card.Complete(At);
        card.ClearDomainEvents();

        var result = card.Reopen(At);

        result.IsSuccess.Should().BeTrue();
        card.IsCompleted.Should().BeFalse();
        card.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CardReopened>();
    }

    [Fact]
    public void Assign_WithNewUser_AddsAndIsIdempotent()
    {
        var card = NewCard();
        var userId = Guid.NewGuid();

        var first = card.Assign(userId, At);
        var second = card.Assign(userId, At);
        var third = card.Assign(userId, At);

        first.IsSuccess.Should().BeTrue();
        second.IsFailure.Should().BeTrue();    // AlreadyAssigned on second call
        second.Error.Code.Should().Be(CardErrors.AlreadyAssigned.Code);
        third.IsFailure.Should().BeTrue();
        card.Members.Should().HaveCount(1);
    }

    [Fact]
    public void Assign_WhenArchived_ReturnsArchivedFailure()
    {
        var card = NewCard();
        card.Archive(At);

        var result = card.Assign(Guid.NewGuid(), At);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CardErrors.Archived.Code);
    }

    [Fact]
    public void Unassign_OfAssignedUser_Removes()
    {
        var card = NewCard();
        var userId = Guid.NewGuid();
        card.Assign(userId, At);

        var result = card.Unassign(userId, At);

        result.IsSuccess.Should().BeTrue();
        card.Members.Should().BeEmpty();
    }

    [Fact]
    public void Unassign_OfNotAssigned_ReturnsNotAssignedFailure()
    {
        var card = NewCard();

        var result = card.Unassign(Guid.NewGuid(), At);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CardErrors.NotAssigned.Code);
    }

    [Fact]
    public void Archive_IsIdempotent()
    {
        var card = NewCard();
        card.Archive(At);
        card.Archive(At);
        card.IsArchived.Should().BeTrue();
    }

    [Fact]
    public void Restore_AfterArchive_Restores()
    {
        var card = NewCard();
        card.Archive(At);

        card.Restore(At);

        card.IsArchived.Should().BeFalse();
    }

    [Fact]
    public void AttachLabel_WithNewLabel_Adds()
    {
        var card = NewCard();
        var link = CardLabel.Create(card.Id, LabelId.New(), At);

        var result = card.AttachLabel(link, At);

        result.IsSuccess.Should().BeTrue();
        card.CardLabels.Should().ContainSingle();
    }

    [Fact]
    public void AttachLabel_WhenAlreadyAttached_IsNoop()
    {
        var card = NewCard();
        var labelId = LabelId.New();
        var link = CardLabel.Create(card.Id, labelId, At);
        card.AttachLabel(link, At);
        card.ClearDomainEvents();

        var result = card.AttachLabel(link, At);

        result.IsSuccess.Should().BeTrue();
        card.CardLabels.Should().HaveCount(1);
    }

    [Fact]
    public void SetCoverColor_UpdatesValue()
    {
        var card = NewCard();
        var color = Color.Create("#ff0000").Value;

        var result = card.SetCoverColor(color, At);

        result.IsSuccess.Should().BeTrue();
        card.CoverColor.Should().Be(color);
    }
}
