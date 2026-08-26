using System.Data.Common;
using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Application.Realtime;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Infrastructure.Persistence.Interceptors;
using Cardscape.Infrastructure.Persistence.Outbox;
using Cardscape.Tests.Common.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cardscape.UnitTests.Infrastructure.Persistence;

public sealed class DomainEventOutboxTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 22, 18, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task SavingChanges_WithOneEventAndTwoBroadcasters_PersistsOneDeliveryPerBroadcasterAndClearsAggregateEvents()
    {
        await using var harness = await OutboxHarness.CreateAsync();
        var card = CreateCard();
        await using CardscapeDbContext db = harness.CreateInterceptedContext();

        db.Cards.Add(card);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        DomainEventOutboxMessage[] deliveries = await db.Set<DomainEventOutboxMessage>()
            .AsNoTracking()
            .OrderBy(message => message.BroadcasterType)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        deliveries.Should().HaveCount(2);
        deliveries.Select(message => message.BroadcasterType).Should().BeEquivalentTo(
            typeof(AlwaysSuccessfulBroadcaster).FullName,
            typeof(ControllableBroadcaster).FullName);
        deliveries.Should().OnlyContain(message =>
            message.EventType == typeof(CardCreated).FullName
            && message.ProcessedAt == Now);
        card.DomainEvents.Should().BeEmpty("events are cleared only after the aggregate and deliveries commit");
        harness.Successful.Events.Should().ContainSingle().Which.Should().BeOfType<CardCreated>();
        harness.Controllable.Events.Should().ContainSingle().Which.Should().BeOfType<CardCreated>();
    }

    [Fact]
    public async Task SavingChanges_WhenOutboxInsertFails_RollsBackAggregateAndPreservesDomainEvents()
    {
        await using var harness = await OutboxHarness.CreateAsync();
        var card = CreateCard();
        await using CardscapeDbContext failingDb = harness.CreateInterceptedContext(
            new RejectOutboxInsertInterceptor());
        failingDb.Cards.Add(card);

        Func<Task> save = () => failingDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        var failure = await save.Should().ThrowAsync<DbUpdateException>();
        failure.Which.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("simulated outbox insert failure");

        await using CardscapeDbContext verificationDb = harness.CreatePlainContext();
        (await verificationDb.Cards.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        (await verificationDb.Set<DomainEventOutboxMessage>()
            .CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        card.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<CardCreated>();
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenBroadcasterFails_RecordsAttemptAndErrorThenSuccessfulRetryMarksProcessed()
    {
        await using var harness = await OutboxHarness.CreateAsync();
        harness.Controllable.Exception = new InvalidOperationException("provider unavailable");
        Guid deliveryId = await harness.SeedAsync(CreateEvent(), typeof(ControllableBroadcaster));

        await harness.Processor.ProcessPendingAsync(TestContext.Current.CancellationToken);

        DomainEventOutboxMessage failed = await harness.ReloadAsync(deliveryId);
        failed.Attempts.Should().Be(1);
        failed.LastError.Should().Be("provider unavailable");
        failed.ProcessedAt.Should().BeNull();
        harness.Controllable.Events.Should().ContainSingle();

        harness.Controllable.Exception = null;
        harness.Clock.Advance(TimeSpan.FromSeconds(2));
        await harness.Processor.ProcessPendingAsync(TestContext.Current.CancellationToken);

        DomainEventOutboxMessage completed = await harness.ReloadAsync(deliveryId);
        completed.Attempts.Should().Be(1, "a successful retry must not erase the delivery history");
        completed.LastError.Should().BeNull();
        completed.ProcessedAt.Should().Be(harness.Clock.UtcNow);
        harness.Controllable.Events.Should().HaveCount(2);
    }

    [Fact]
    public async Task ProcessPendingAsync_WithIndependentBroadcasters_FailureDoesNotBlockSuccessfulDelivery()
    {
        await using var harness = await OutboxHarness.CreateAsync();
        harness.Controllable.Exception = new InvalidOperationException("isolated failure");
        CardCreated @event = CreateEvent();
        Guid failingId = await harness.SeedAsync(@event, typeof(ControllableBroadcaster));
        Guid successfulId = await harness.SeedAsync(@event, typeof(AlwaysSuccessfulBroadcaster));

        await harness.Processor.ProcessPendingAsync(TestContext.Current.CancellationToken);

        DomainEventOutboxMessage failing = await harness.ReloadAsync(failingId);
        DomainEventOutboxMessage successful = await harness.ReloadAsync(successfulId);
        failing.ProcessedAt.Should().BeNull();
        failing.Attempts.Should().Be(1);
        failing.LastError.Should().Be("isolated failure");
        successful.ProcessedAt.Should().Be(Now);
        successful.Attempts.Should().Be(0);
        successful.LastError.Should().BeNull();
        harness.Controllable.Events.Should().ContainSingle();
        harness.Successful.Events.Should().ContainSingle();
    }

    [Fact]
    public void SerializeAndDeserialize_CardCreated_RoundTripsConcreteEventAndTypedValues()
    {
        CardCreated original = CreateEvent();

        (string eventType, string json) = DomainEventOutboxSerializer.Serialize(original);
        IDomainEvent deserialized = DomainEventOutboxSerializer.Deserialize(eventType, json);

        eventType.Should().Be(typeof(CardCreated).FullName);
        CardCreated roundTrip = deserialized.Should().BeOfType<CardCreated>().Subject;
        roundTrip.CardId.Should().Be(original.CardId);
        roundTrip.ListId.Should().Be(original.ListId);
        roundTrip.Title.Should().Be(original.Title);
        roundTrip.OccurredAt.Should().Be(original.OccurredAt);
    }

    private static Card CreateCard() => Card.Create(
        new CardId(Guid.Parse("1a10290b-dd3f-47ae-9140-77a43888b4b2")),
        new BoardListId(Guid.Parse("07740e0b-9255-486a-808d-b4ac04ffed4a")),
        CardTitle.Create("Transactional outbox").Value,
        CardDescription.Create("Persist before broadcasting").Value,
        Position.Start(),
        Guid.Parse("271965ac-4587-4563-a58e-a112c82d188f"),
        Now).Value;

    private static CardCreated CreateEvent() => new(
        new CardId(Guid.Parse("1a10290b-dd3f-47ae-9140-77a43888b4b2")),
        new BoardListId(Guid.Parse("07740e0b-9255-486a-808d-b4ac04ffed4a")),
        CardTitle.Create("Transactional outbox").Value,
        Now);

    private sealed class AlwaysSuccessfulBroadcaster : IDomainEventBroadcaster
    {
        public List<IDomainEvent> Events { get; } = [];

        public Task BroadcastAsync(IDomainEvent @event, CancellationToken ct = default)
        {
            Events.Add(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class ControllableBroadcaster : IDomainEventBroadcaster
    {
        public Exception? Exception { get; set; }
        public List<IDomainEvent> Events { get; } = [];

        public Task BroadcastAsync(IDomainEvent @event, CancellationToken ct = default)
        {
            Events.Add(@event);
            return Exception is null ? Task.CompletedTask : Task.FromException(Exception);
        }
    }

    private sealed class OutboxHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _services;

        private OutboxHarness(
            SqliteConnection connection,
            ServiceProvider services,
            FakeClock clock,
            AlwaysSuccessfulBroadcaster successful,
            ControllableBroadcaster controllable)
        {
            _connection = connection;
            _services = services;
            Clock = clock;
            Successful = successful;
            Controllable = controllable;
            Processor = new DomainEventOutboxProcessor(
                services.GetRequiredService<IServiceScopeFactory>(),
                clock,
                NullLogger<DomainEventOutboxProcessor>.Instance);
        }

        public FakeClock Clock { get; }
        public AlwaysSuccessfulBroadcaster Successful { get; }
        public ControllableBroadcaster Controllable { get; }
        public DomainEventOutboxProcessor Processor { get; }

        public static async Task<OutboxHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var clock = new FakeClock(Now);
            var successful = new AlwaysSuccessfulBroadcaster();
            var controllable = new ControllableBroadcaster();
            var services = new ServiceCollection()
                .AddSingleton(connection)
                .AddScoped(serviceProvider => new CardscapeDbContext(
                    new DbContextOptionsBuilder<CardscapeDbContext>()
                        .UseSqlite(serviceProvider.GetRequiredService<SqliteConnection>())
                        .Options))
                .AddSingleton<IDomainEventBroadcaster>(successful)
                .AddSingleton<IDomainEventBroadcaster>(controllable)
                .BuildServiceProvider(validateScopes: true);
            var harness = new OutboxHarness(connection, services, clock, successful, controllable);
            await using CardscapeDbContext db = harness.CreatePlainContext();
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            return harness;
        }

        public CardscapeDbContext CreatePlainContext() => new(
            new DbContextOptionsBuilder<CardscapeDbContext>().UseSqlite(_connection).Options);

        public CardscapeDbContext CreateInterceptedContext(DbCommandInterceptor? commandInterceptor = null)
        {
            var interceptor = new DomainEventsInterceptor(
                [Successful, Controllable],
                Processor,
                Clock,
                NullLogger<DomainEventsInterceptor>.Instance);
            var options = new DbContextOptionsBuilder<CardscapeDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(interceptor);
            if (commandInterceptor is not null)
            {
                options.AddInterceptors(commandInterceptor);
            }

            return new CardscapeDbContext(options.Options);
        }

        public async Task<Guid> SeedAsync(CardCreated @event, Type broadcasterType)
        {
            (string eventType, string payloadJson) = DomainEventOutboxSerializer.Serialize(@event);
            DomainEventOutboxMessage message = DomainEventOutboxMessage.Create(
                eventType,
                payloadJson,
                broadcasterType.FullName!,
                @event.OccurredAt,
                Clock.UtcNow);
            await using CardscapeDbContext db = CreatePlainContext();
            db.Add(message);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return message.Id;
        }

        public async Task<DomainEventOutboxMessage> ReloadAsync(Guid id)
        {
            await using CardscapeDbContext db = CreatePlainContext();
            return await db.Set<DomainEventOutboxMessage>()
                .AsNoTracking()
                .SingleAsync(message => message.Id == id, TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _services.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class RejectOutboxInsertInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("domain_event_outbox", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("simulated outbox insert failure");
            }

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
