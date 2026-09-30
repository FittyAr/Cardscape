using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.DependencyInjection;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Tests.Common.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cardscape.IntegrationTests.Persistence;

/// <summary>Runs on SQLite ordinarily; CI explicitly selects each isolated external engine.</summary>
[Trait("Category", "Provider")]
public sealed class ProviderPersistenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProviderConfiguration_ModelDriftIsAnError()
    {
        await using ServiceProvider services = CreateServices();
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        CardscapeDbContext db = scope.ServiceProvider.GetRequiredService<CardscapeDbContext>();
        CoreOptionsExtension options = db.GetService<IDbContextOptions>().Extensions
            .OfType<CoreOptionsExtension>().Single();
        options.WarningsConfiguration.GetBehavior(RelationalEventId.PendingModelChangesWarning)
            .Should().Be(WarningBehavior.Throw);
    }

    [Fact]
    public async Task ProviderMigrations_ApplyWithoutDriftAndPersistDomainValues()
    {
        await using ServiceProvider services = CreateServices();
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        CardscapeDbContext db = scope.ServiceProvider.GetRequiredService<CardscapeDbContext>();
        string expectedDriver = (Environment.GetEnvironmentVariable("CARDSCAPE_TEST_PROVIDER") ?? "Sqlite") switch
        {
            "Sqlite" => "Microsoft.Data.Sqlite.SqliteConnection",
            "PostgreSQL" => "Npgsql.NpgsqlConnection",
            "MySql" => "MySql.Data.MySqlClient.MySqlConnection",
            "MariaDB" => "MySqlConnector.MySqlConnection",
            _ => throw new InvalidOperationException("Unknown provider gate selection.")
        };
        db.Database.GetDbConnection().GetType().FullName.Should().Be(expectedDriver);
        await db.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        db.Database.GetMigrations().Should().NotBeEmpty();
        db.Database.HasPendingModelChanges().Should().BeFalse();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        (await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();

        User user = CreateUser();
        Workspace workspace = Workspace.Create(WorkspaceId.New(), WorkspaceName.Create("Equipo Ñ 日本語").Value,
            user.Id.Value, Region.Europe, Now).Value;
        db.Users.Add(user);
        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        Workspace persisted = await db.Workspaces.AsNoTracking().Include(x => x.Members)
            .SingleAsync(x => x.Id == workspace.Id, TestContext.Current.CancellationToken);
        persisted.Name.Value.Should().Be("Equipo Ñ 日本語");
        persisted.OwnerId.Should().Be(user.Id.Value);
        persisted.Region.Should().Be(Region.Europe);
        persisted.CreatedAt.Should().Be(Now);
        persisted.RowVersion.Should().Be(0);
        persisted.Members.Should().ContainSingle().Which.UserId.Should().Be(user.Id.Value);
        User savedUser = await db.Users.AsNoTracking().SingleAsync(x => x.Id == user.Id, TestContext.Current.CancellationToken);
        savedUser.Email.Value.Should().Be(user.Email.Value);
        savedUser.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ProviderConcurrency_StaleUpdateIsRejected()
    {
        await using ServiceProvider services = CreateServices();
        await using AsyncServiceScope seedScope = services.CreateAsyncScope();
        CardscapeDbContext seed = seedScope.ServiceProvider.GetRequiredService<CardscapeDbContext>();
        await seed.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await seed.Database.MigrateAsync(TestContext.Current.CancellationToken);
        User user = CreateUser();
        seed.Users.Add(user);
        await seed.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using AsyncServiceScope firstScope = services.CreateAsyncScope();
        await using AsyncServiceScope secondScope = services.CreateAsyncScope();
        CardscapeDbContext first = firstScope.ServiceProvider.GetRequiredService<CardscapeDbContext>();
        CardscapeDbContext second = secondScope.ServiceProvider.GetRequiredService<CardscapeDbContext>();
        User winner = await first.Users.SingleAsync(x => x.Id == user.Id, TestContext.Current.CancellationToken);
        User stale = await second.Users.SingleAsync(x => x.Id == user.Id, TestContext.Current.CancellationToken);
        winner.UpdateProfile(DisplayName.Create("Winner").Value, null, Now).IsSuccess.Should().BeTrue();
        await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        stale.UpdateProfile(DisplayName.Create("Stale").Value, null, Now).IsSuccess.Should().BeTrue();
        Func<Task> save = () => second.SaveChangesAsync(TestContext.Current.CancellationToken);
        await save.Should().ThrowAsync<DbUpdateConcurrencyException>();
        first.ChangeTracker.Clear();
        User persisted = await first.Users.AsNoTracking().SingleAsync(x => x.Id == user.Id, TestContext.Current.CancellationToken);
        persisted.DisplayName.Value.Should().Be("Winner");
        persisted.RowVersion.Should().Be(1);
    }

    private static ServiceProvider CreateServices()
    {
        string provider = Environment.GetEnvironmentVariable("CARDSCAPE_TEST_PROVIDER") ?? "Sqlite";
        string connection = provider == "Sqlite"
            ? $"Data Source=provider-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
            : Environment.GetEnvironmentVariable("CARDSCAPE_TEST_CONNECTION")
                ?? throw new InvalidOperationException("External provider tests require CARDSCAPE_TEST_CONNECTION.");
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = provider,
            ["ConnectionStrings:Default"] = connection
        }).Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IClock>(new FakeClock(Now));
        services.AddCardscapeInfrastructure(configuration);
        // Replace external delivery channels, retaining the real transactional outbox.
        services.RemoveAll<IDomainEventBroadcaster>();
        services.AddSingleton<IDomainEventBroadcaster, PersistenceBroadcaster>();
        return services.BuildServiceProvider();
    }

    private sealed class PersistenceBroadcaster : IDomainEventBroadcaster
    {
        public Task BroadcastAsync(IDomainEvent domainEvent, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private static User CreateUser() => User.RegisterExternal(UserId.New(),
        EmailAddress.Create($"provider-{Guid.NewGuid():N}@cardscape.local").Value,
        DisplayName.Create("Provider Member").Value, Now).Value;
}
