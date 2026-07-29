using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Recurrence;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Recurrence;
using Cardscape.Tests.Common.Fakes;
using FluentAssertions;
using Xunit;

namespace Cardscape.UnitTests.Recurrence;

public class CardRecurrenceTests
{
    [Fact]
    public void Create_With_Zero_Interval_Fails()
    {
        var create = CardRecurrence.Create(
            CardRecurrenceId.New(), CardId.New(),
            intervalDays: 0, nextOccurrenceAt: DateTimeOffset.UtcNow.AddDays(1),
            createdBy: Guid.NewGuid(), at: DateTimeOffset.UtcNow);
        create.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_With_Too_Long_Interval_Fails()
    {
        var create = CardRecurrence.Create(
            CardRecurrenceId.New(), CardId.New(),
            intervalDays: 500, nextOccurrenceAt: DateTimeOffset.UtcNow.AddDays(500),
            createdBy: Guid.NewGuid(), at: DateTimeOffset.UtcNow);
        create.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_With_Empty_Creator_Fails()
    {
        var create = CardRecurrence.Create(
            CardRecurrenceId.New(), CardId.New(),
            intervalDays: 7, nextOccurrenceAt: DateTimeOffset.UtcNow.AddDays(7),
            createdBy: Guid.Empty, at: DateTimeOffset.UtcNow);
        create.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_With_Valid_Inputs_Succeeds_And_Sets_Active()
    {
        var create = CardRecurrence.Create(
            CardRecurrenceId.New(), CardId.New(),
            intervalDays: 7, nextOccurrenceAt: DateTimeOffset.UtcNow.AddDays(7),
            createdBy: Guid.NewGuid(), at: DateTimeOffset.UtcNow);
        create.IsSuccess.Should().BeTrue();
        create.Value.IntervalDays.Should().Be(7);
        create.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Update_Changes_Interval_And_Next_Occurrence()
    {
        var rule = CardRecurrence.Create(
            CardRecurrenceId.New(), CardId.New(),
            intervalDays: 7, nextOccurrenceAt: DateTimeOffset.UtcNow.AddDays(7),
            createdBy: Guid.NewGuid(), at: DateTimeOffset.UtcNow).Value;
        var newOccurrence = DateTimeOffset.UtcNow.AddDays(14);

        var result = rule.Update(14, newOccurrence, DateTimeOffset.UtcNow);
        result.IsSuccess.Should().BeTrue();
        rule.IntervalDays.Should().Be(14);
        rule.NextOccurrenceAt.Should().Be(newOccurrence);
    }

    [Fact]
    public void Deactivate_Sets_IsActive_To_False()
    {
        var rule = CardRecurrence.Create(
            CardRecurrenceId.New(), CardId.New(),
            intervalDays: 7, nextOccurrenceAt: DateTimeOffset.UtcNow.AddDays(7),
            createdBy: Guid.NewGuid(), at: DateTimeOffset.UtcNow).Value;

        rule.Deactivate(DateTimeOffset.UtcNow);
        rule.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Set_Persists_A_New_Recurrence()
    {
        var ctx = new HandlersTestContext();
        var owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value);
        var list = await ctx.SeedListAsync(board.Id);
        var card = await ctx.SeedCardAsync(list.Id, owner.Id.Value);

        var recurrences = new InMemoryCardRecurrenceRepository();
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(owner);

        var result = await SetCardRecurrenceCommandHandler.Handle(
            new SetCardRecurrenceCommand(card.Id.Value, 7, DateTimeOffset.UtcNow.AddDays(7)),
            recurrences, ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.CurrentUser, ctx.UnitOfWork, ctx.Clock, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IntervalDays.Should().Be(7);
        result.Value.IsActive.Should().BeTrue();
        recurrences.All.Should().HaveCount(1);
    }

    [Fact]
    public async Task Set_Twice_Updates_Instead_Of_Creating()
    {
        var ctx = new HandlersTestContext();
        var owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value);
        var list = await ctx.SeedListAsync(board.Id);
        var card = await ctx.SeedCardAsync(list.Id, owner.Id.Value);

        var recurrences = new InMemoryCardRecurrenceRepository();
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(owner);

        await SetCardRecurrenceCommandHandler.Handle(
            new SetCardRecurrenceCommand(card.Id.Value, 7, DateTimeOffset.UtcNow.AddDays(7)),
            recurrences, ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.CurrentUser, ctx.UnitOfWork, ctx.Clock, CancellationToken.None);
        var second = await SetCardRecurrenceCommandHandler.Handle(
            new SetCardRecurrenceCommand(card.Id.Value, 14, DateTimeOffset.UtcNow.AddDays(14)),
            recurrences, ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.CurrentUser, ctx.UnitOfWork, ctx.Clock, CancellationToken.None);

        second.Value.IntervalDays.Should().Be(14);
        recurrences.All.Should().HaveCount(1);
    }

    [Fact]
    public async Task Delete_Deactivates_The_Rule()
    {
        var ctx = new HandlersTestContext();
        var owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value);
        var list = await ctx.SeedListAsync(board.Id);
        var card = await ctx.SeedCardAsync(list.Id, owner.Id.Value);

        var recurrences = new InMemoryCardRecurrenceRepository();
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(owner);

        await SetCardRecurrenceCommandHandler.Handle(
            new SetCardRecurrenceCommand(card.Id.Value, 7, DateTimeOffset.UtcNow.AddDays(7)),
            recurrences, ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.CurrentUser, ctx.UnitOfWork, ctx.Clock, CancellationToken.None);

        var del = await DeleteCardRecurrenceCommandHandler.Handle(
            new DeleteCardRecurrenceCommand(card.Id.Value),
            recurrences, ctx.UnitOfWork, ctx.Clock, CancellationToken.None);
        del.IsSuccess.Should().BeTrue();
        recurrences.All.Should().BeEmpty();
    }
}
