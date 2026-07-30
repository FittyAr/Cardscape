using Cardscape.Application.Cards.Events;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.GoogleCalendar;
using Cardscape.Tests.Common.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cardscape.UnitTests.Application.Handlers;

/// <summary>
/// G8 — push path tests for <see cref="CardDueDateCalendarSync"/>.
/// </summary>
public class CardDueDateCalendarSyncTests
{
    [Fact]
    public async Task Handle_SetDueDate_ForConnectedUser_PushesAndRecordsSuccess()
    {
        var ctx = new HandlersTestContext();
        var owner = await ctx.SeedUserAsync();
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value);
        var list = await ctx.SeedListAsync(board.Id);
        var card = await ctx.SeedCardAsync(list.Id, owner.Id.Value, "Buy milk");

        var connResult = GoogleCalendarConnection.Establish(
            GoogleCalendarConnectionId.New(),
            owner.Id.Value,
            workspace.Id,
            googleEmail: "owner@gmail.com",
            encryptedRefreshToken: "rt",
            calendarId: "primary",
            at: ctx.Clock.UtcNow);
        await ctx.GoogleCalendarConnections.AddAsync(connResult.Value);

        await CardDueDateCalendarSync.Handle(
            new CardDueDateSet(card.Id, ctx.Clock.UtcNow.AddDays(7), ctx.Clock.UtcNow),
            ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.GoogleCalendarConnections, ctx.GoogleCalendarSync,
            NullLogger.Instance, CancellationToken.None);

        ctx.GoogleCalendarSync.PushCalls.Should().HaveCount(1);
        ctx.GoogleCalendarSync.PushCalls[0].UserId.Should().Be(owner.Id.Value);
        ctx.GoogleCalendarSync.PushCalls[0].CardId.Should().Be(card.Id.Value);
        ctx.GoogleCalendarSync.PushCalls[0].DueDate.Should().NotBeNull();

        var updated = await ctx.GoogleCalendarConnections.FindByUserAsync(owner.Id.Value);
        updated!.LastSyncedAt.Should().NotBeNull();
        updated.LastSyncError.Should().BeNull();
    }

    [Fact]
    public async Task Handle_SetDueDate_WithPushFailure_RecordsError()
    {
        var ctx = new HandlersTestContext();
        var owner = await ctx.SeedUserAsync();
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value);
        var list = await ctx.SeedListAsync(board.Id);
        var card = await ctx.SeedCardAsync(list.Id, owner.Id.Value, "Buy milk");

        var connResult = GoogleCalendarConnection.Establish(
            GoogleCalendarConnectionId.New(),
            owner.Id.Value,
            workspace.Id,
            googleEmail: "owner@gmail.com",
            encryptedRefreshToken: "rt",
            calendarId: "primary",
            at: ctx.Clock.UtcNow);
        await ctx.GoogleCalendarConnections.AddAsync(connResult.Value);

        ctx.GoogleCalendarSync.NextPushResult = Result.Failure<string>(
            Cardscape.Domain.Common.DomainError.External(
                "google_calendar.500",
                "Google Calendar push failed (500): boom"));

        await CardDueDateCalendarSync.Handle(
            new CardDueDateSet(card.Id, ctx.Clock.UtcNow.AddDays(7), ctx.Clock.UtcNow),
            ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.GoogleCalendarConnections, ctx.GoogleCalendarSync,
            NullLogger.Instance, CancellationToken.None);

        var updated = await ctx.GoogleCalendarConnections.FindByUserAsync(owner.Id.Value);
        updated!.LastSyncedAt.Should().BeNull();
        updated.LastSyncError.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_SetDueDate_WithNoConnection_IsNoop()
    {
        var ctx = new HandlersTestContext();
        var owner = await ctx.SeedUserAsync();
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value);
        var list = await ctx.SeedListAsync(board.Id);
        var card = await ctx.SeedCardAsync(list.Id, owner.Id.Value, "Buy milk");

        await CardDueDateCalendarSync.Handle(
            new CardDueDateSet(card.Id, ctx.Clock.UtcNow.AddDays(7), ctx.Clock.UtcNow),
            ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.GoogleCalendarConnections, ctx.GoogleCalendarSync,
            NullLogger.Instance, CancellationToken.None);

        ctx.GoogleCalendarSync.PushCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ClearDueDate_PushesWithNullDueDate()
    {
        var ctx = new HandlersTestContext();
        var owner = await ctx.SeedUserAsync();
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value);
        var list = await ctx.SeedListAsync(board.Id);
        var card = await ctx.SeedCardAsync(list.Id, owner.Id.Value, "Buy milk");

        var connResult = GoogleCalendarConnection.Establish(
            GoogleCalendarConnectionId.New(),
            owner.Id.Value,
            workspace.Id,
            googleEmail: "owner@gmail.com",
            encryptedRefreshToken: "rt",
            calendarId: "primary",
            at: ctx.Clock.UtcNow);
        await ctx.GoogleCalendarConnections.AddAsync(connResult.Value);

        await CardDueDateCalendarSync.Handle(
            new CardDueDateCleared(card.Id, ctx.Clock.UtcNow),
            ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.GoogleCalendarConnections, ctx.GoogleCalendarSync,
            NullLogger.Instance, CancellationToken.None);

        ctx.GoogleCalendarSync.PushCalls.Should().HaveCount(1);
        ctx.GoogleCalendarSync.PushCalls[0].DueDate.Should().BeNull();
    }

    [Fact]
    public async Task Handle_MultipleConnectedUsers_FansOutToEveryUser()
    {
        var ctx = new HandlersTestContext();
        var owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        var member = await ctx.SeedUserAsync("member@example.com", "Member");
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value);
        var list = await ctx.SeedListAsync(board.Id);
        var card = await ctx.SeedCardAsync(list.Id, owner.Id.Value, "Buy milk");

        foreach (var user in new[] { owner, member })
        {
            var connResult = GoogleCalendarConnection.Establish(
                GoogleCalendarConnectionId.New(),
                user.Id.Value,
                workspace.Id,
                googleEmail: user.Email.Value,
                encryptedRefreshToken: "rt",
                calendarId: "primary",
                at: ctx.Clock.UtcNow);
            await ctx.GoogleCalendarConnections.AddAsync(connResult.Value);
        }

        await CardDueDateCalendarSync.Handle(
            new CardDueDateSet(card.Id, ctx.Clock.UtcNow.AddDays(7), ctx.Clock.UtcNow),
            ctx.Cards, ctx.Lists, ctx.Boards,
            ctx.GoogleCalendarConnections, ctx.GoogleCalendarSync,
            NullLogger.Instance, CancellationToken.None);

        ctx.GoogleCalendarSync.PushCalls.Should().HaveCount(2);
    }
}
