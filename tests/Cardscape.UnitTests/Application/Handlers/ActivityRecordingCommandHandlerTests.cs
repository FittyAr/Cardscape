using System.Text.Json;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Storage;
using Cardscape.Application.Attachments;
using Cardscape.Application.Boards.Commands;
using Cardscape.Application.Cards.Commands;
using Cardscape.Application.Checklists;
using Cardscape.Application.Lists.Commands;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Attachments;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using Cardscape.Tests.Common.Fakes;
using Moq;
using Color = Cardscape.Domain.Common.Color;

namespace Cardscape.UnitTests.Application.Handlers;

/// <summary>
/// Board, list, due-date, label, attachment and checklist commands record
/// their activity at runtime, so a real instance shows the same history
/// the seeded demo does. No-op commands (renaming to the same name,
/// archiving twice, clearing an empty due date) record nothing.
/// </summary>
public class ActivityRecordingCommandHandlerTests
{
    private sealed record Scenario(HandlersTestContext Ctx, Guid UserId, Board Board, BoardList List, Card Card);

    private static async Task<Scenario> SeedAsync()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        var workspace = await ctx.SeedWorkspaceAsync(user.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, user.Id.Value, "Roadmap");
        var list = await ctx.SeedListAsync(board.Id, "To Do");
        var card = await ctx.SeedCardAsync(list.Id, user.Id.Value);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(user);
        return new Scenario(ctx, user.Id.Value, board, list, card);
    }

    private static Activity Single(Scenario s, ActivityKind kind)
    {
        var activity = s.Ctx.Activities.All.Should().ContainSingle().Subject;
        activity.Kind.Should().Be(kind);
        activity.BoardId.Should().Be(s.Board.Id);
        activity.ActorId.Should().Be(s.UserId);
        activity.OccurredAt.Should().Be(s.Ctx.Clock.UtcNow);
        return activity;
    }

    private static JsonElement Payload(Activity activity) =>
        JsonDocument.Parse(activity.PayloadJson).RootElement;

    // ── boards ──────────────────────────────────────────────

    [Fact]
    public async Task RenameBoard_RecordsBoardRenamedWithNewName()
    {
        var s = await SeedAsync();

        var result = await RenameBoardCommandHandler.HandleAsync(
            new RenameBoardCommand(s.Board.Id.Value, "Roadmap 2027"),
            s.Ctx.Boards, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, s.Ctx.Activities, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var activity = Single(s, ActivityKind.BoardRenamed);
        activity.CardId.Should().BeNull();
        Payload(activity).GetProperty("name").GetString().Should().Be("Roadmap 2027");
    }

    [Fact]
    public async Task RenameBoard_ToSameName_RecordsNothing()
    {
        var s = await SeedAsync();

        await RenameBoardCommandHandler.HandleAsync(
            new RenameBoardCommand(s.Board.Id.Value, "Roadmap"),
            s.Ctx.Boards, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, s.Ctx.Activities, CancellationToken.None);

        s.Ctx.Activities.All.Should().BeEmpty();
    }

    [Fact]
    public async Task ArchiveBoard_RecordsOnce_AndUnarchiveRecordsBoardUnarchived()
    {
        var s = await SeedAsync();

        await ArchiveBoardCommandHandler.HandleAsync(
            new ArchiveBoardCommand(s.Board.Id.Value),
            s.Ctx.Boards, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, s.Ctx.Activities, CancellationToken.None);
        await ArchiveBoardCommandHandler.HandleAsync(
            new ArchiveBoardCommand(s.Board.Id.Value),
            s.Ctx.Boards, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, s.Ctx.Activities, CancellationToken.None);

        var archived = Single(s, ActivityKind.BoardArchived);
        Payload(archived).GetProperty("name").GetString().Should().Be("Roadmap");

        await UnarchiveBoardCommandHandler.HandleAsync(
            new UnarchiveBoardCommand(s.Board.Id.Value),
            s.Ctx.Boards, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, s.Ctx.Activities, CancellationToken.None);

        s.Ctx.Activities.All.Select(a => a.Kind).Should()
            .BeEquivalentTo([ActivityKind.BoardArchived, ActivityKind.BoardUnarchived]);
    }

    // ── lists ───────────────────────────────────────────────

    [Fact]
    public async Task CreateList_RecordsListCreatedWithName()
    {
        var s = await SeedAsync();

        var result = await CreateListCommandHandler.HandleAsync(
            new CreateListCommand(s.Board.Id.Value, "Review"),
            s.Ctx.Boards, s.Ctx.Lists, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, s.Ctx.Activities, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var payload = Payload(Single(s, ActivityKind.ListCreated));
        payload.GetProperty("listId").GetGuid().Should().Be(result.Value.Id);
        payload.GetProperty("name").GetString().Should().Be("Review");
    }

    [Fact]
    public async Task RenameList_RecordsListRenamed_UnlessNameUnchanged()
    {
        var s = await SeedAsync();

        await RenameListCommandHandler.HandleAsync(
            new RenameListCommand(s.List.Id.Value, "To Do"),
            s.Ctx.Lists, s.Ctx.Boards, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, s.Ctx.Activities, CancellationToken.None);
        s.Ctx.Activities.All.Should().BeEmpty();

        await RenameListCommandHandler.HandleAsync(
            new RenameListCommand(s.List.Id.Value, "Backlog"),
            s.Ctx.Lists, s.Ctx.Boards, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, s.Ctx.Activities, CancellationToken.None);

        Payload(Single(s, ActivityKind.ListRenamed)).GetProperty("name").GetString().Should().Be("Backlog");
    }

    [Fact]
    public async Task MoveList_RecordsListMovedWithNameAndPosition()
    {
        var s = await SeedAsync();

        await MoveListCommandHandler.HandleAsync(
            new MoveListCommand(s.List.Id.Value, 3),
            s.Ctx.Lists, s.Ctx.Boards, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, s.Ctx.Activities, CancellationToken.None);

        var payload = Payload(Single(s, ActivityKind.ListMoved));
        payload.GetProperty("name").GetString().Should().Be("To Do");
        payload.GetProperty("position").GetDouble().Should().Be(3);
    }

    [Fact]
    public async Task ArchiveList_RecordsListArchivedOnce()
    {
        var s = await SeedAsync();

        for (int i = 0; i < 2; i++)
        {
            await ArchiveListCommandHandler.HandleAsync(
                new ArchiveListCommand(s.List.Id.Value),
                s.Ctx.Lists, s.Ctx.Boards, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, s.Ctx.Activities, CancellationToken.None);
        }

        Payload(Single(s, ActivityKind.ListArchived)).GetProperty("name").GetString().Should().Be("To Do");
    }

    // ── due dates ───────────────────────────────────────────

    [Fact]
    public async Task SetAndClearDueDate_RecordBothKindsOnTheCard()
    {
        var s = await SeedAsync();
        DateTimeOffset due = s.Ctx.Clock.UtcNow.AddDays(3);

        await SetCardDueDateCommandHandler.HandleAsync(
            new SetCardDueDateCommand(s.Card.Id.Value, due),
            s.Ctx.Cards, s.Ctx.Lists, s.Ctx.Boards, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, s.Ctx.Activities, CancellationToken.None);

        var set = Single(s, ActivityKind.CardDueDateSet);
        set.CardId.Should().Be(s.Card.Id.Value);
        Payload(set).GetProperty("dueDate").GetDateTimeOffset().Should().Be(due);

        await ClearCardDueDateCommandHandler.HandleAsync(
            new ClearCardDueDateCommand(s.Card.Id.Value),
            s.Ctx.Cards, s.Ctx.Lists, s.Ctx.Boards, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, s.Ctx.Activities, CancellationToken.None);
        await ClearCardDueDateCommandHandler.HandleAsync(
            new ClearCardDueDateCommand(s.Card.Id.Value),
            s.Ctx.Cards, s.Ctx.Lists, s.Ctx.Boards, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, s.Ctx.Activities, CancellationToken.None);

        var cleared = s.Ctx.Activities.All.Should().ContainSingle(a => a.Kind == ActivityKind.CardDueDateCleared).Subject;
        cleared.CardId.Should().Be(s.Card.Id.Value);
        Payload(cleared).GetProperty("previousDueDate").GetDateTimeOffset().Should().Be(due);
    }

    // ── labels ──────────────────────────────────────────────

    [Fact]
    public async Task AttachAndDetachLabel_RecordLabelAddedAndRemovedWithName()
    {
        var s = await SeedAsync();
        var label = Label.Create(LabelId.New(), s.Board.Id, LabelName.Create("Urgent").Value,
            Color.Palette.Orange, s.UserId, s.Ctx.Clock.UtcNow).Value;
        await s.Ctx.Labels.AddAsync(label, TestContext.Current.CancellationToken);

        for (int i = 0; i < 2; i++)
        {
            await AttachLabelToCardCommandHandler.HandleAsync(
                new AttachLabelToCardCommand(s.Card.Id.Value, label.Id.Value),
                s.Ctx.Cards, s.Ctx.Lists, s.Ctx.Boards, s.Ctx.Labels, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, s.Ctx.Activities, CancellationToken.None);
        }

        var added = Single(s, ActivityKind.LabelAdded);
        added.CardId.Should().Be(s.Card.Id.Value);
        Payload(added).GetProperty("name").GetString().Should().Be("Urgent");

        await DetachLabelFromCardCommandHandler.HandleAsync(
            new DetachLabelFromCardCommand(s.Card.Id.Value, label.Id.Value),
            s.Ctx.Cards, s.Ctx.Lists, s.Ctx.Boards, s.Ctx.Labels, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, s.Ctx.Activities, CancellationToken.None);

        var removed = s.Ctx.Activities.All.Should().ContainSingle(a => a.Kind == ActivityKind.LabelRemoved).Subject;
        Payload(removed).GetProperty("labelId").GetGuid().Should().Be(label.Id.Value);
        Payload(removed).GetProperty("name").GetString().Should().Be("Urgent");
    }

    // ── attachments ─────────────────────────────────────────

    [Fact]
    public async Task DeleteAttachment_RecordsAttachmentRemovedWithFileName()
    {
        var s = await SeedAsync();
        var attachment = Attachment.Create(AttachmentId.New(), s.Card.Id, "spec.pdf", "application/pdf", 3,
            "cards/x/spec.pdf", s.UserId, s.Ctx.Clock.UtcNow).Value;
        var attachments = new Mock<IAttachmentRepository>();
        attachments.Setup(x => x.GetByIdAsync(attachment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(attachment);

        var result = await DeleteAttachmentCommandHandler.HandleAsync(
            new DeleteAttachmentCommand(s.Card.Id.Value, attachment.Id.Value),
            attachments.Object, s.Ctx.Cards, s.Ctx.Lists, s.Ctx.Boards, s.Ctx.UnitOfWork,
            Mock.Of<IStorageService>(), s.Ctx.Clock, s.Ctx.CurrentUser, s.Ctx.Activities, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var activity = Single(s, ActivityKind.AttachmentRemoved);
        activity.CardId.Should().Be(s.Card.Id.Value);
        Payload(activity).GetProperty("fileName").GetString().Should().Be("spec.pdf");
    }

    // ── checklists ──────────────────────────────────────────

    [Fact]
    public async Task CreateChecklist_RecordsChecklistCreatedWithTitle()
    {
        var s = await SeedAsync();

        var result = await CreateChecklistCommandHandler.HandleAsync(
            new CreateChecklistCommand(s.Card.Id.Value, "Launch steps"),
            s.Ctx.Checklists, s.Ctx.Cards, s.Ctx.Lists, s.Ctx.Boards,
            s.Ctx.CurrentUser, s.Ctx.UnitOfWork, s.Ctx.Clock, s.Ctx.Activities, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var activity = Single(s, ActivityKind.ChecklistCreated);
        activity.CardId.Should().Be(s.Card.Id.Value);
        var payload = Payload(activity);
        payload.GetProperty("checklistId").GetGuid().Should().Be(result.Value.Id);
        payload.GetProperty("title").GetString().Should().Be("Launch steps");
    }
}
