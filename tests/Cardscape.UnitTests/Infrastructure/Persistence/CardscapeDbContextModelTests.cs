using Cardscape.Application.Realtime;
using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Infrastructure.Persistence.Interceptors;
using Cardscape.Infrastructure.Persistence.Outbox;
using Cardscape.Domain.BackgroundJobs;
using Cardscape.Domain.Notifications;
using Cardscape.Tests.Common.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cardscape.UnitTests.Infrastructure.Persistence;

public sealed class CardscapeDbContextModelTests
{
    [Fact]
    public void EveryMappedRowVersion_IsAConcurrencyTokenWithZeroDefault()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        var options = new DbContextOptionsBuilder<CardscapeDbContext>()
            .UseSqlite(connection)
            .Options;
        using var dbContext = new CardscapeDbContext(options);

        var rowVersions = dbContext.Model.GetEntityTypes()
            .Select(entityType => new
            {
                EntityName = entityType.DisplayName(),
                Property = entityType.FindProperty("RowVersion"),
            })
            .Where(candidate => candidate.Property is not null)
            .ToArray();

        rowVersions.Should().NotBeEmpty("the persistence model must expose optimistic concurrency tokens");

        var violations = rowVersions
            .Where(candidate =>
                !candidate.Property!.IsConcurrencyToken
                || !Equals(candidate.Property.GetDefaultValue(), 0u))
            .Select(candidate =>
                $"{candidate.EntityName}.RowVersion "
                + $"(concurrency token: {candidate.Property!.IsConcurrencyToken}, "
                + $"default: {candidate.Property.GetDefaultValue() ?? "<null>"})")
            .ToArray();

        violations.Should().BeEmpty(
            "every mapped RowVersion property must be an optimistic concurrency token with default 0");
    }

    [Fact]
    public async Task SavingModifiedEntities_AdvancesRowVersionExactlyOnce()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var clock = new FakeClock();
        await using ServiceProvider services = new ServiceCollection()
            .AddSingleton(connection)
            .AddScoped(serviceProvider => new CardscapeDbContext(
                new DbContextOptionsBuilder<CardscapeDbContext>()
                    .UseSqlite(serviceProvider.GetRequiredService<SqliteConnection>())
                    .Options))
            .BuildServiceProvider(validateScopes: true);
        var processor = new DomainEventOutboxProcessor(
            services.GetRequiredService<IServiceScopeFactory>(),
            clock,
            NullLogger<DomainEventOutboxProcessor>.Instance);
        var interceptor = new DomainEventsInterceptor(
            Array.Empty<IDomainEventBroadcaster>(),
            processor,
            clock,
            NullLogger<DomainEventsInterceptor>.Instance);
        var options = new DbContextOptionsBuilder<CardscapeDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;
        await using var dbContext = new CardscapeDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var now = DateTimeOffset.UtcNow;
        var stamped = BackgroundJob.Enqueue("test:stamped", "{}", now, 3, now).Value;
        var fallback = Notification.Create(
            Guid.NewGuid(), NotificationKind.Mentioned, "{}", now);
        dbContext.AddRange(stamped, fallback);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        stamped.TryClaim(now).Should().BeTrue();
        fallback.MarkRead(now);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        stamped.RowVersion.Should().Be(1u,
            "StampChanged already advanced the token and persistence must not double-increment it");
        fallback.RowVersion.Should().Be(1u,
            "persistence must provide the fallback increment for an unstamped mutation");
    }
}
