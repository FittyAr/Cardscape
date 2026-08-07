using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Checklists;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Common;
using Cardscape.Tests.Common.Fakes;
using FluentAssertions;
using Xunit;

namespace Cardscape.UnitTests.Checklists;

public class ChecklistTests
{
    [Fact]
    public void Create_With_Empty_Creator_Fails()
    {
        var create = Checklist.Create(
            ChecklistId.New(), CardId.New(),
            ChecklistTitle.Create("todo").Value,
            createdBy: Guid.Empty,
            at: DateTimeOffset.UtcNow);
        create.IsFailure.Should().BeTrue();
        create.Error.Code.Should().Be("checklists.creator_required");
    }

    [Fact]
    public void Create_With_Valid_Inputs_Succeeds_And_Adds_Domain_Event()
    {
        var create = Checklist.Create(
            ChecklistId.New(), CardId.New(),
            ChecklistTitle.Create("todo").Value,
            createdBy: Guid.NewGuid(),
            at: DateTimeOffset.UtcNow);
        create.IsSuccess.Should().BeTrue();
        create.Value.Title.Value.Should().Be("todo");
    }

    [Fact]
    public void AddItem_Appends_And_Increases_Count()
    {
        var cl = Checklist.Create(
            ChecklistId.New(), CardId.New(),
            ChecklistTitle.Create("c").Value,
            Guid.NewGuid(), DateTimeOffset.UtcNow).Value;

        cl.AddItem(ChecklistItemText.Create("a").Value, Position.From(1), DateTimeOffset.UtcNow);
        cl.AddItem(ChecklistItemText.Create("b").Value, Position.From(2), DateTimeOffset.UtcNow);

        cl.Items.Should().HaveCount(2);
    }

    [Fact]
    public void CheckItem_Marks_As_Completed()
    {
        var cl = Checklist.Create(
            ChecklistId.New(), CardId.New(),
            ChecklistTitle.Create("c").Value,
            Guid.NewGuid(), DateTimeOffset.UtcNow).Value;
        cl.AddItem(ChecklistItemText.Create("a").Value, Position.From(1), DateTimeOffset.UtcNow);

        var item = cl.Items.First();
        var result = cl.CheckItem(item.Id, DateTimeOffset.UtcNow);
        result.IsSuccess.Should().BeTrue();
        item.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void UncheckItem_Clears_Completion()
    {
        var cl = Checklist.Create(
            ChecklistId.New(), CardId.New(),
            ChecklistTitle.Create("c").Value,
            Guid.NewGuid(), DateTimeOffset.UtcNow).Value;
        cl.AddItem(ChecklistItemText.Create("a").Value, Position.From(1), DateTimeOffset.UtcNow);
        var item = cl.Items.First();
        cl.CheckItem(item.Id, DateTimeOffset.UtcNow);
        var result = cl.UncheckItem(item.Id, DateTimeOffset.UtcNow);
        result.IsSuccess.Should().BeTrue();
        item.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void RemoveItem_Takes_It_Out_Of_The_Collection()
    {
        var cl = Checklist.Create(
            ChecklistId.New(), CardId.New(),
            ChecklistTitle.Create("c").Value,
            Guid.NewGuid(), DateTimeOffset.UtcNow).Value;
        cl.AddItem(ChecklistItemText.Create("a").Value, Position.From(1), DateTimeOffset.UtcNow);
        var item = cl.Items.First();

        cl.RemoveItem(item.Id, DateTimeOffset.UtcNow);
        cl.Items.Should().BeEmpty();
    }

    [Fact]
    public void UpdateItem_Changes_The_Text()
    {
        var cl = Checklist.Create(
            ChecklistId.New(), CardId.New(),
            ChecklistTitle.Create("c").Value,
            Guid.NewGuid(), DateTimeOffset.UtcNow).Value;
        cl.AddItem(ChecklistItemText.Create("a").Value, Position.From(1), DateTimeOffset.UtcNow);
        var item = cl.Items.First();

        var result = cl.UpdateItem(item.Id, ChecklistItemText.Create("b").Value, DateTimeOffset.UtcNow);
        result.IsSuccess.Should().BeTrue();
        item.Text.Value.Should().Be("b");
    }

    [Fact]
    public void Rename_Updates_The_Title()
    {
        var cl = Checklist.Create(
            ChecklistId.New(), CardId.New(),
            ChecklistTitle.Create("c").Value,
            Guid.NewGuid(), DateTimeOffset.UtcNow).Value;

        var result = cl.Rename(ChecklistTitle.Create("new").Value, DateTimeOffset.UtcNow);
        result.IsSuccess.Should().BeTrue();
        cl.Title.Value.Should().Be("new");
    }

    [Fact]
    public void Delete_Marks_As_Soft_Deleted()
    {
        var cl = Checklist.Create(
            ChecklistId.New(), CardId.New(),
            ChecklistTitle.Create("c").Value,
            Guid.NewGuid(), DateTimeOffset.UtcNow).Value;

        cl.Delete(DateTimeOffset.UtcNow);
        cl.IsDeleted.Should().BeTrue();
    }

    // ── handler tests ──────────────────────────────────────

    [Fact]
    public async Task Create_Checklist_Persists_And_Returns_Dto()
    {
        var ctx = new HandlersTestContext();
        var owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value);
        var list = await ctx.SeedListAsync(board.Id);
        var card = await ctx.SeedCardAsync(list.Id, owner.Id.Value);

        var checklists = new InMemoryChecklistRepository();
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(owner);

        var result = await CreateChecklistCommandHandler.Handle(
            new CreateChecklistCommand(card.Id.Value, "My checklist"),
            checklists, ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.CurrentUser, ctx.UnitOfWork, ctx.Clock, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("My checklist");
        checklists.All.Should().HaveCount(1);
    }

    [Fact]
    public async Task Add_Item_Then_Toggle_Persists_And_Reports_State()
    {
        var ctx = new HandlersTestContext();
        var owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value);
        var list = await ctx.SeedListAsync(board.Id);
        var card = await ctx.SeedCardAsync(list.Id, owner.Id.Value);

        var checklists = new InMemoryChecklistRepository();
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(owner);

        var created = await CreateChecklistCommandHandler.Handle(
            new CreateChecklistCommand(card.Id.Value, "Todos"),
            checklists, ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.CurrentUser, ctx.UnitOfWork, ctx.Clock, CancellationToken.None);

        var withItem = await AddChecklistItemCommandHandler.Handle(
            new AddChecklistItemCommand(created.Value!.Id, "Buy milk"),
            checklists, ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, ctx.SearchIndex, ctx.Activities, CancellationToken.None);

        withItem.IsSuccess.Should().BeTrue();
        withItem.Value!.Items.Should().HaveCount(1);
        Guid itemId = withItem.Value.Items[0].Id;

        var toggled = await ToggleChecklistItemCommandHandler.Handle(
            new ToggleChecklistItemCommand(created.Value.Id, itemId),
            checklists, ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, ctx.Activities, CancellationToken.None);

        toggled.IsSuccess.Should().BeTrue();
        toggled.Value!.CompletedCount.Should().Be(1);
        toggled.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task List_Returns_Checklist_With_Items_And_Counts()
    {
        var ctx = new HandlersTestContext();
        var owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value);
        var list = await ctx.SeedListAsync(board.Id);
        var card = await ctx.SeedCardAsync(list.Id, owner.Id.Value);

        var checklists = new InMemoryChecklistRepository();
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(owner);

        var created = await CreateChecklistCommandHandler.Handle(
            new CreateChecklistCommand(card.Id.Value, "c"),
            checklists, ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.CurrentUser, ctx.UnitOfWork, ctx.Clock, CancellationToken.None);
        await AddChecklistItemCommandHandler.Handle(
            new AddChecklistItemCommand(created.Value!.Id, "x"),
            checklists, ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, ctx.SearchIndex, ctx.Activities, CancellationToken.None);
        await AddChecklistItemCommandHandler.Handle(
            new AddChecklistItemCommand(created.Value.Id, "y"),
            checklists, ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, ctx.SearchIndex, ctx.Activities, CancellationToken.None);

        var listed = await ListCardChecklistsQueryHandler.Handle(
            new ListCardChecklistsQuery(card.Id.Value),
            checklists, ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.CurrentUser, CancellationToken.None);

        listed.IsSuccess.Should().BeTrue();
        listed.Value!.Should().HaveCount(1);
        listed.Value[0].Items.Should().HaveCount(2);
        listed.Value[0].CompletedCount.Should().Be(0);
        listed.Value[0].TotalCount.Should().Be(2);
    }
}
