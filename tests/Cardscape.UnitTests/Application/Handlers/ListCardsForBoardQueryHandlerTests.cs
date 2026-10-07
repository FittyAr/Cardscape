using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Cards.Queries;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Comments;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Cardscape.Tests.Common.Fakes;

namespace Cardscape.UnitTests.Application.Handlers;

public class ListCardsForBoardQueryHandlerTests
{
    [Fact]
    public async Task Handle_ProjectsLabelsMembersChecklistProgressAndCommentCount()
    {
        var ctx = new HandlersTestContext();
        var owner = await ctx.SeedUserAsync("owner@example.com", "Ada Lovelace");
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value);
        var list = await ctx.SeedListAsync(board.Id);
        var card = await ctx.SeedCardAsync(list.Id, owner.Id.Value, "Busy card");
        var bare = await ctx.SeedCardAsync(list.Id, owner.Id.Value, "Bare card");
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(owner);

        var label = Label.Create(LabelId.New(), board.Id, LabelName.Create("Bug").Value,
            Color.Palette.Red, owner.Id.Value, ctx.Clock.UtcNow).Value;
        await ctx.Labels.AddAsync(label, TestContext.Current.CancellationToken);
        card.AttachLabel(CardLabel.Create(card.Id, label.Id, ctx.Clock.UtcNow), ctx.Clock.UtcNow);
        card.Assign(owner.Id.Value, ctx.Clock.UtcNow);

        var checklist = Checklist.Create(ChecklistId.New(), card.Id, ChecklistTitle.Create("Todo").Value,
            owner.Id.Value, ctx.Clock.UtcNow).Value;
        var done = checklist.AddItem(ChecklistItemText.Create("one").Value, Position.Start(), ctx.Clock.UtcNow);
        checklist.AddItem(ChecklistItemText.Create("two").Value, Position.From(2), ctx.Clock.UtcNow);
        checklist.AddItem(ChecklistItemText.Create("three").Value, Position.From(3), ctx.Clock.UtcNow);
        checklist.CheckItem(done.Id, ctx.Clock.UtcNow);
        await ctx.Checklists.AddAsync(checklist, TestContext.Current.CancellationToken);

        for (int i = 0; i < 2; i++)
        {
            await ctx.Comments.AddAsync(Comment.Create(CommentId.New(), card.Id, owner.Id.Value,
                CommentBody.Create($"c{i}").Value, ctx.Clock.UtcNow).Value, TestContext.Current.CancellationToken);
        }

        var result = await ListCardsForBoardQueryHandler.HandleAsync(
            new ListCardsForBoardQuery(board.Id.Value),
            ctx.Cards, new EmptySnoozes(), new EmptyMirrors(), ctx.Boards,
            ctx.Labels, ctx.Users, ctx.Checklists, ctx.Comments,
            ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        CardSummaryDto busy = result.Value.Single(c => c.Id == card.Id.Value);
        busy.Labels.Should().ContainSingle()
            .Which.Should().Be(new CardSummaryLabelDto(label.Id.Value, "Bug", Color.Palette.Red.Value));
        busy.Members.Should().ContainSingle()
            .Which.Should().Be(new CardSummaryMemberDto(owner.Id.Value, "Ada Lovelace"));
        busy.ChecklistCompleted.Should().Be(1);
        busy.ChecklistTotal.Should().Be(3);
        busy.CommentCount.Should().Be(2);

        CardSummaryDto empty = result.Value.Single(c => c.Id == bare.Id.Value);
        empty.Labels.Should().BeEmpty();
        empty.Members.Should().BeEmpty();
        empty.ChecklistTotal.Should().Be(0);
        empty.CommentCount.Should().Be(0);
    }

    private sealed class EmptySnoozes : ICardSnoozeRepository
    {
        public Task<CardSnooze?> GetByCardIdAsync(CardId cardId, CancellationToken ct = default) =>
            Task.FromResult<CardSnooze?>(null);

        public Task<IReadOnlyList<CardSnooze>> ListActiveAsync(DateTimeOffset now, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CardSnooze>>([]);

        public Task<IReadOnlyList<CardSnooze>> ListForBoardAsync(Guid boardId, DateTimeOffset now, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CardSnooze>>([]);

        public Task AddAsync(CardSnooze snooze, CancellationToken ct = default) => Task.CompletedTask;

        public Task RemoveAsync(CardSnooze snooze, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class EmptyMirrors : ICardMirrorRepository
    {
        public Task<CardMirror?> GetByMirroredCardIdAsync(CardId mirroredCardId, CancellationToken ct = default) =>
            Task.FromResult<CardMirror?>(null);

        public Task<IReadOnlyList<CardMirror>> ListForSourceAsync(CardId sourceCardId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CardMirror>>([]);

        public Task<IReadOnlyList<CardMirror>> ListForBoardAsync(Guid boardId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CardMirror>>([]);

        public Task AddAsync(CardMirror mirror, CancellationToken ct = default) => Task.CompletedTask;

        public Task RemoveAsync(CardMirror mirror, CancellationToken ct = default) => Task.CompletedTask;
    }
}
