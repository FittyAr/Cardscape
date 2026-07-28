using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Voting;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Voting;
using Cardscape.Tests.Common.Fakes;
using FluentAssertions;
using Xunit;

namespace Cardscape.UnitTests.Voting;

public class CardVoteTests
{
    [Fact]
    public void Create_With_Empty_User_Fails()
    {
        var clock = new FakeClock();
        var create = CardVote.Create(CardVoteId.New(), CardId.New(), Guid.Empty, clock.UtcNow);
        create.IsFailure.Should().BeTrue();
        create.Error.Code.Should().Be("votes.user_required");
    }

    [Fact]
    public void Create_With_Valid_User_Succeeds_And_Stores_CardId()
    {
        var clock = new FakeClock();
        CardId cardId = CardId.New();
        Guid userId = Guid.NewGuid();

        var create = CardVote.Create(CardVoteId.New(), cardId, userId, clock.UtcNow);
        create.IsSuccess.Should().BeTrue();
        create.Value.CardId.Value.Should().Be(cardId.Value);
        create.Value.UserId.Should().Be(userId);
        create.Value.VotedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task Toggle_First_Time_Adds_One_Vote_And_Reports_HasVoted()
    {
        var ctx = new HandlersTestContext();
        var member = await ctx.SeedUserAsync("member@example.com", "Member");
        var owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value);
        board.AddMember(member.Id.Value, BoardMemberRole.Member, ctx.Clock.UtcNow);
        var list = await ctx.SeedListAsync(board.Id);
        var card = await ctx.SeedCardAsync(list.Id, owner.Id.Value);

        var votes = new InMemoryCardVoteRepository();
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(member);

        var result = await ToggleCardVoteCommandHandler.Handle(
            new ToggleCardVoteCommand(card.Id.Value),
            votes, ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.CurrentUser, ctx.UnitOfWork, ctx.Clock, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.VoteCount.Should().Be(1);
        result.Value.CurrentUserHasVoted.Should().BeTrue();
    }

    [Fact]
    public async Task Toggle_Second_Time_Removes_The_Vote()
    {
        var ctx = new HandlersTestContext();
        var member = await ctx.SeedUserAsync("member@example.com", "Member");
        var owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value);
        board.AddMember(member.Id.Value, BoardMemberRole.Member, ctx.Clock.UtcNow);
        var list = await ctx.SeedListAsync(board.Id);
        var card = await ctx.SeedCardAsync(list.Id, owner.Id.Value);

        var votes = new InMemoryCardVoteRepository();
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(member);

        await ToggleCardVoteCommandHandler.Handle(
            new ToggleCardVoteCommand(card.Id.Value),
            votes, ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.CurrentUser, ctx.UnitOfWork, ctx.Clock, CancellationToken.None);

        var result = await ToggleCardVoteCommandHandler.Handle(
            new ToggleCardVoteCommand(card.Id.Value),
            votes, ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.CurrentUser, ctx.UnitOfWork, ctx.Clock, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.VoteCount.Should().Be(0);
        result.Value.CurrentUserHasVoted.Should().BeFalse();
    }

    [Fact]
    public async Task Toggle_By_Non_Member_Returns_Forbidden()
    {
        var ctx = new HandlersTestContext();
        var owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        var stranger = await ctx.SeedUserAsync("stranger@example.com", "Stranger");
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value);
        var list = await ctx.SeedListAsync(board.Id);
        var card = await ctx.SeedCardAsync(list.Id, owner.Id.Value);

        var votes = new InMemoryCardVoteRepository();
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(stranger);

        var result = await ToggleCardVoteCommandHandler.Handle(
            new ToggleCardVoteCommand(card.Id.Value),
            votes, ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.CurrentUser, ctx.UnitOfWork, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Toggle_On_Unknown_Card_Returns_NotFound()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync("user@example.com", "User");
        var votes = new InMemoryCardVoteRepository();
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(user);

        var result = await ToggleCardVoteCommandHandler.Handle(
            new ToggleCardVoteCommand(Guid.NewGuid()),
            votes, ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.CurrentUser, ctx.UnitOfWork, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task List_For_Card_Reports_Zero_And_NotVoted_For_Fresh_Card()
    {
        var ctx = new HandlersTestContext();
        var member = await ctx.SeedUserAsync("member@example.com", "Member");
        var owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value);
        board.AddMember(member.Id.Value, BoardMemberRole.Member, ctx.Clock.UtcNow);
        var list = await ctx.SeedListAsync(board.Id);
        var card = await ctx.SeedCardAsync(list.Id, owner.Id.Value);

        var votes = new InMemoryCardVoteRepository();
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(member);

        var result = await ListCardVotesQueryHandler.Handle(
            new ListCardVotesQuery(card.Id.Value),
            votes, ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.CurrentUser, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.VoteCount.Should().Be(0);
        result.Value.CurrentUserHasVoted.Should().BeFalse();
    }

    [Fact]
    public async Task List_After_Toggle_Reflects_New_State()
    {
        var ctx = new HandlersTestContext();
        var member = await ctx.SeedUserAsync("member@example.com", "Member");
        var owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value);
        board.AddMember(member.Id.Value, BoardMemberRole.Member, ctx.Clock.UtcNow);
        var list = await ctx.SeedListAsync(board.Id);
        var card = await ctx.SeedCardAsync(list.Id, owner.Id.Value);

        var votes = new InMemoryCardVoteRepository();
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(member);

        await ToggleCardVoteCommandHandler.Handle(
            new ToggleCardVoteCommand(card.Id.Value),
            votes, ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.CurrentUser, ctx.UnitOfWork, ctx.Clock, CancellationToken.None);

        var result = await ListCardVotesQueryHandler.Handle(
            new ListCardVotesQuery(card.Id.Value),
            votes, ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.CurrentUser, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.VoteCount.Should().Be(1);
        result.Value.CurrentUserHasVoted.Should().BeTrue();
    }
}
