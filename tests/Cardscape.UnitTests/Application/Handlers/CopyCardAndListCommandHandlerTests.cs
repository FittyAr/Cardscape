using Cardscape.Application.Cards.Commands;
using Cardscape.Application.Lists.Commands;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using Cardscape.Tests.Common.Fakes;

namespace Cardscape.UnitTests.Application.Handlers;

public class CopyCardAndListCommandHandlerTests
{
    private sealed record Scenario(
        HandlersTestContext Ctx,
        Guid UserId,
        BoardList Source,
        BoardList Target,
        Card Card,
        LabelId LabelId,
        Guid OtherMemberId);

    /// <summary>A board with two lists and one fully dressed card in the first.</summary>
    private static async Task<Scenario> SeedAsync()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        var workspace = await ctx.SeedWorkspaceAsync(user.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, user.Id.Value);
        var source = await ctx.SeedListAsync(board.Id, "To Do");
        var target = await ctx.SeedListAsync(board.Id, "Doing");
        target.Move(Position.From(2), ctx.Clock.UtcNow);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(user);

        DateTimeOffset at = ctx.Clock.UtcNow;
        var card = await ctx.SeedCardAsync(source.Id, user.Id.Value, "Original");
        var labelId = LabelId.New();
        Guid otherMember = Guid.NewGuid();
        card.AttachLabel(CardLabel.Create(card.Id, labelId, at), at);
        card.Assign(user.Id.Value, at);
        card.Assign(otherMember, at);
        card.SetCoverColor(Color.Palette.Orange, at);

        var checklist = Checklist.Create(ChecklistId.New(), card.Id, ChecklistTitle.Create("Steps").Value, user.Id.Value, at).Value;
        var done = checklist.AddItem(ChecklistItemText.Create("First").Value, Position.From(1), at);
        checklist.AddItem(ChecklistItemText.Create("Second").Value, Position.From(2), at);
        checklist.CheckItem(done.Id, at);
        await ctx.Checklists.AddAsync(checklist);

        return new Scenario(ctx, user.Id.Value, source, target, card, labelId, otherMember);
    }

    private static Task<Result<Cardscape.Application.Cards.DTOs.CardDto>> CopyCardAsync(
        HandlersTestContext ctx, CopyCardCommand command) =>
        CopyCardCommandHandler.HandleAsync(
            command, ctx.Cards, ctx.Lists, ctx.Boards, ctx.Checklists, ctx.UnitOfWork,
            ctx.CurrentUser, ctx.Clock, ctx.Activities, CancellationToken.None);

    private static Task<Result<Cardscape.Application.Lists.DTOs.BoardListDto>> CopyListAsync(
        HandlersTestContext ctx, CopyListCommand command) =>
        CopyListCommandHandler.HandleAsync(
            command, ctx.Boards, ctx.Lists, ctx.Cards, ctx.Checklists, ctx.UnitOfWork,
            ctx.CurrentUser, ctx.Clock, CancellationToken.None);

    [Fact]
    public async Task CopyCard_CopiesContentLabelsMembersAndChecklists()
    {
        var s = await SeedAsync();

        var result = await CopyCardAsync(s.Ctx, new CopyCardCommand(s.Card.Id.Value, s.Target.Id.Value, "Duplicate", null));

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(s.Card.Id.Value);
        result.Value.ListId.Should().Be(s.Target.Id.Value);
        result.Value.Title.Should().Be("Duplicate");
        result.Value.Description.Should().Be(s.Card.Description.Value);
        result.Value.CoverColor.Should().Be(Color.Palette.Orange.Value);
        result.Value.LabelIds.Should().Equal(s.LabelId.Value);
        result.Value.MemberIds.Should().Equal(s.UserId, s.OtherMemberId);

        var copiedChecklists = await s.Ctx.Checklists.ListForCardAsync(result.Value.Id, TestContext.Current.CancellationToken);
        copiedChecklists.Should().ContainSingle();
        var items = copiedChecklists[0].Items.OrderBy(i => i.Position.Value).ToList();
        items.Select(i => i.Text.Value).Should().Equal("First", "Second");
        items.Select(i => i.IsCompleted).Should().Equal(true, false);

        // The source card keeps its own checklist and is untouched.
        (await s.Ctx.Checklists.ListForCardAsync(s.Card.Id.Value, TestContext.Current.CancellationToken)).Should().ContainSingle();
        s.Card.ListId.Should().Be(s.Source.Id);
        s.Ctx.Activities.All.Should().ContainSingle();
    }

    [Fact]
    public async Task CopyCard_WithoutTitleOrPosition_KeepsTitleAndAppendsToBottom()
    {
        var s = await SeedAsync();

        var result = await CopyCardAsync(s.Ctx, new CopyCardCommand(s.Card.Id.Value, s.Source.Id.Value, null, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Original");
        result.Value.Position.Should().BeGreaterThan(s.Card.Position.Value);
    }

    [Fact]
    public async Task CopyCard_ToListOnAnotherBoard_IsRejected()
    {
        var s = await SeedAsync();
        var otherBoard = await s.Ctx.SeedBoardAsync(WorkspaceId.New(), s.UserId, "Elsewhere");
        var foreignList = await s.Ctx.SeedListAsync(otherBoard.Id);

        var result = await CopyCardAsync(s.Ctx, new CopyCardCommand(s.Card.Id.Value, foreignList.Id.Value, null, null));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("cards.invalid_copy");
        s.Ctx.Cards.All.Should().ContainSingle();
    }

    [Fact]
    public async Task CopyCard_ToArchivedList_IsRejected()
    {
        var s = await SeedAsync();
        s.Target.Archive(s.Ctx.Clock.UtcNow);

        var result = await CopyCardAsync(s.Ctx, new CopyCardCommand(s.Card.Id.Value, s.Target.Id.Value, null, null));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("cards.copy_to_archived_list");
    }

    [Fact]
    public async Task CopyCard_AsNonMember_IsRejected()
    {
        var s = await SeedAsync();
        var intruder = await s.Ctx.SeedUserAsync("intruder@example.com", "Intruder");
        s.Ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(intruder);

        var result = await CopyCardAsync(s.Ctx, new CopyCardCommand(s.Card.Id.Value, s.Target.Id.Value, null, null));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        s.Ctx.Cards.All.Should().ContainSingle();
    }

    [Fact]
    public async Task CopyList_PlacesCopyBetweenSourceAndNextList_AndCopiesOpenCards()
    {
        var s = await SeedAsync();
        var archived = await s.Ctx.SeedCardAsync(s.Source.Id, s.UserId, "Old news");
        archived.Archive(s.Ctx.Clock.UtcNow);

        var result = await CopyListAsync(s.Ctx, new CopyListCommand(s.Source.Id.Value, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("To Do");
        result.Value.CardCount.Should().Be(1);
        result.Value.Position.Should().BeGreaterThan(s.Source.Position.Value)
            .And.BeLessThan(s.Target.Position.Value);

        var copies = await s.Ctx.Cards.ListForListAsync(new BoardListId(result.Value.Id), includeArchived: true, TestContext.Current.CancellationToken);
        var copy = copies.Should().ContainSingle().Subject;
        copy.Title.Value.Should().Be("Original");
        copy.CardLabels.Select(l => l.LabelId).Should().Equal(s.LabelId);
        copy.Members.Should().HaveCount(2);
        (await s.Ctx.Checklists.ListForCardAsync(copy.Id.Value, TestContext.Current.CancellationToken)).Should().ContainSingle()
            .Which.Items.Should().HaveCount(2);

        // The source list still holds its own cards.
        (await s.Ctx.Cards.ListForListAsync(s.Source.Id, includeArchived: true, TestContext.Current.CancellationToken)).Should().HaveCount(2);
    }

    [Fact]
    public async Task CopyList_OfLastList_WithCustomName_AppendsAfterIt()
    {
        var s = await SeedAsync();

        var result = await CopyListAsync(s.Ctx, new CopyListCommand(s.Target.Id.Value, "Doing (copy)"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Doing (copy)");
        result.Value.CardCount.Should().Be(0);
        result.Value.Position.Should().BeGreaterThan(s.Target.Position.Value);
        s.Ctx.UnitOfWork.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task CopyList_AsNonMember_IsRejected()
    {
        var s = await SeedAsync();
        var intruder = await s.Ctx.SeedUserAsync("intruder@example.com", "Intruder");
        s.Ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(intruder);

        var result = await CopyListAsync(s.Ctx, new CopyListCommand(s.Source.Id.Value, null));

        result.IsFailure.Should().BeTrue();
        s.Ctx.Lists.All.Should().HaveCount(2);
    }
}
