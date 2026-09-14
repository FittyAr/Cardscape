using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Infrastructure.Persistence.Inbox;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.IntegrationTests.Inbox;

public sealed class ExternalMessageInboxTests
{
    [Fact]
    public async Task BeginAsync_WhileLeaseActive_ReturnsInProgress()
    {
        await using var database = await InboxDatabase.CreateAsync();
        DateTimeOffset now = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

        ExternalMessageReservation acquired = await database.CreateInbox().BeginAsync(
            "inbound-email:sendgrid", "message-hash", now, TestContext.Current.CancellationToken);
        ExternalMessageReservation duplicate = await database.CreateInbox().BeginAsync(
            "inbound-email:sendgrid", "message-hash", now.AddMinutes(1), TestContext.Current.CancellationToken);

        acquired.State.Should().Be(ExternalMessageReservationState.Acquired);
        duplicate.Should().Be(new ExternalMessageReservation(
            acquired.ReceiptId, ExternalMessageReservationState.InProgress));
    }

    [Fact]
    public async Task BeginAsync_AfterCompletedReceipt_ReplaysResourceId()
    {
        await using var database = await InboxDatabase.CreateAsync();
        DateTimeOffset now = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);
        Guid resourceId = Guid.NewGuid();
        ExternalMessageReservation acquired = await database.CreateInbox().BeginAsync(
            "inbound-email:mailgun", "message-hash", now, TestContext.Current.CancellationToken);

        bool completed = await database.CreateInbox().CompleteAsync(
            acquired.ReceiptId, resourceId, now.AddMinutes(1), TestContext.Current.CancellationToken);
        ExternalMessageReservation replay = await database.CreateInbox().BeginAsync(
            "inbound-email:mailgun", "message-hash", now.AddDays(1), TestContext.Current.CancellationToken);

        completed.Should().BeTrue();
        replay.Should().Be(new ExternalMessageReservation(
            acquired.ReceiptId, ExternalMessageReservationState.Completed, resourceId));
    }

    [Fact]
    public async Task BeginAsync_AfterExpiredLease_ReacquiresReservation()
    {
        await using var database = await InboxDatabase.CreateAsync();
        DateTimeOffset now = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);
        ExternalMessageReservation expired = await database.CreateInbox().BeginAsync(
            "inbound-email:postmark", "message-hash", now, TestContext.Current.CancellationToken);

        ExternalMessageReservation reacquired = await database.CreateInbox().BeginAsync(
            "inbound-email:postmark", "message-hash", now.AddMinutes(16), TestContext.Current.CancellationToken);

        reacquired.State.Should().Be(ExternalMessageReservationState.Acquired);
        reacquired.ReceiptId.Should().NotBe(expired.ReceiptId);
    }

    [Fact]
    public async Task ReleaseAsync_AfterFailedProcessing_AllowsImmediateRetry()
    {
        await using var database = await InboxDatabase.CreateAsync();
        DateTimeOffset now = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);
        ExternalMessageReservation failed = await database.CreateInbox().BeginAsync(
            "inbound-email:sendgrid", "message-hash", now, TestContext.Current.CancellationToken);

        await database.CreateInbox().ReleaseAsync(failed.ReceiptId, TestContext.Current.CancellationToken);
        ExternalMessageReservation retry = await database.CreateInbox().BeginAsync(
            "inbound-email:sendgrid", "message-hash", now.AddSeconds(1), TestContext.Current.CancellationToken);

        retry.State.Should().Be(ExternalMessageReservationState.Acquired);
        retry.ReceiptId.Should().NotBe(failed.ReceiptId);
    }

    private sealed class InboxDatabase : IAsyncDisposable
    {
        private readonly string _path;
        private readonly DbContextOptions<CardscapeDbContext> _options;
        private readonly List<CardscapeDbContext> _contexts = [];

        private InboxDatabase(string path, DbContextOptions<CardscapeDbContext> options)
        {
            _path = path;
            _options = options;
        }

        public static async Task<InboxDatabase> CreateAsync()
        {
            string path = Path.Combine(Path.GetTempPath(), $"cardscape-inbox-{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<CardscapeDbContext>()
                .UseSqlite($"Data Source={path};Default Timeout=30")
                .Options;
            await using var setup = new CardscapeDbContext(options);
            await setup.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            return new InboxDatabase(path, options);
        }

        public ExternalMessageInbox CreateInbox()
        {
            var context = new CardscapeDbContext(_options);
            _contexts.Add(context);
            return new ExternalMessageInbox(context);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (CardscapeDbContext context in _contexts)
            {
                await context.DisposeAsync();
            }

            SqliteConnection.ClearAllPools();
            File.Delete(_path);
        }
    }
}
