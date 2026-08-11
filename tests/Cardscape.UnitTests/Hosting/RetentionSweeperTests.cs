using Cardscape.Application.Abstractions;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Infrastructure.Hosting;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Tests.Common.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cardscape.UnitTests.Hosting;

/// <summary>
/// Regression coverage for the GDPR retention sweeper. The
/// sweeper's <c>SweepOnceAsync</c> had a LINQ-to-SQL
/// translation bug that crashed the hosted service every
/// tick in production (the docker log captured the
/// <c>InvalidOperationException: The LINQ expression … could
/// not be translated</c> stack trace). The fix replaced the
/// <c>EF.Property&lt;bool&gt;(u, "IsDeleted")</c> access with
/// the plain <c>u.IsDeleted</c> access; this test pins the
/// query so a future regression that re-introduces the
/// metadata-access / member-access mix (or breaks the
/// predicate in any other way) fires a deterministic test
/// failure on SQLite instead of a runtime exception under
/// load.
///
/// <para>The test uses a real, file-based SQLite database
/// (one file per test) so the LINQ expression is actually
/// translated to SQL — an in-memory EF provider would
/// client-evaluate the predicate and silently swallow the
/// very bug this test is here to catch.</para>
/// </summary>
public class RetentionSweeperTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SweepOnceAsync_AnonymisesUsersPastGracePeriod()
    {
        // Arrange — fresh SQLite file per test, migrated to
        // the same schema the production API uses.
        string dbPath = Path.Combine(
            Path.GetTempPath(), $"retention-{Guid.NewGuid():N}.db");
        try
        {
            // The sweeper pulls a scoped DbContext out of an
            // IServiceProvider, so we build a small DI
            // container with the SQLite-registered context
            // and resolve the sweeper through it. This is
            // the same composition root the production
            // BackgroundService uses, minus the rest of the
            // app's services.
            ServiceProvider sp = BuildProvider(dbPath);
            try
            {
                // Lift the user ids to the outer scope so the
                // assertion phase can reload each user after
                // the sweep. The User instances themselves
                // are tracked by the seed context and
                // disposed when that scope closes — the
                // DbContext refresh below reads the row
                // fresh from SQLite.
                UserId pastGraceId;
                UserId alreadyAnonymisedId;
                UserId withinGraceId;
                UserId notDeletedId;

                using (IServiceScope seedScope = sp.CreateScope())
                {
                    CardscapeDbContext db = seedScope.ServiceProvider
                        .GetRequiredService<CardscapeDbContext>();
                    db.Database.Migrate();

                    User pastGrace = SeedUser(Now, deletedDaysAgo: 31,
                        isDeleted: true, isAnonymised: false);
                    User alreadyAnonymised = SeedUser(Now, deletedDaysAgo: 31,
                        isDeleted: true, isAnonymised: true);
                    User withinGrace = SeedUser(Now, deletedDaysAgo: 10,
                        isDeleted: true, isAnonymised: false);
                    User notDeleted = SeedUser(Now, deletedDaysAgo: null,
                        isDeleted: false, isAnonymised: false);
                    db.Users.Add(pastGrace);
                    db.Users.Add(alreadyAnonymised);
                    db.Users.Add(withinGrace);
                    db.Users.Add(notDeleted);
                    await db.SaveChangesAsync(TestContext.Current.CancellationToken);

                    pastGraceId = pastGrace.Id;
                    alreadyAnonymisedId = alreadyAnonymised.Id;
                    withinGraceId = withinGrace.Id;
                    notDeletedId = notDeleted.Id;

                    // Sanity: the four seed users are persisted
                    // and in their pre-sweep states.
                    User pastGraceReloaded = await Reload(db, pastGraceId);
                    User alreadyAnonymisedReloaded = await Reload(db, alreadyAnonymisedId);
                    User withinGraceReloaded = await Reload(db, withinGraceId);
                    User notDeletedReloaded = await Reload(db, notDeletedId);

                    pastGraceReloaded.IsDeleted.Should().BeTrue();
                    pastGraceReloaded.IsAnonymised.Should().BeFalse();
                    alreadyAnonymisedReloaded.IsDeleted.Should().BeTrue();
                    alreadyAnonymisedReloaded.IsAnonymised.Should().BeTrue();
                    withinGraceReloaded.IsDeleted.Should().BeTrue();
                    withinGraceReloaded.IsAnonymised.Should().BeFalse();
                    notDeletedReloaded.IsDeleted.Should().BeFalse();
                    notDeletedReloaded.IsAnonymised.Should().BeFalse();
                }

                // Act — drive one sweep with a pinned clock
                // and a 30-day grace period. The sweeper
                // resolves the DbContext through its own
                // IServiceScope, so we don't need to pass
                // the context in.
                var clock = new FakeClock(Now);
                var settings = Options.Create(new RetentionSettingsOptions
                {
                    UserGracePeriodDays = 30,
                    BatchSize = 100
                });
                var sweeper = new RetentionSweeper(
                    sp, clock, settings, NullLogger<RetentionSweeper>.Instance);

                // This call is the one that used to throw
                // InvalidOperationException at line 119 of
                // the production code. If a future
                // regression re-introduces a non-translatable
                // predicate, the assertion phase of this
                // test never runs and the exception bubbles
                // up as a test failure — which is the
                // failure mode we want.
                await sweeper.SweepOnceAsync(TestContext.Current.CancellationToken);

                // Assert — re-open a fresh scope so the
                // assertions observe a clean second-level
                // cache, not the in-memory state from the
                // seed step.
                using IServiceScope assertScope = sp.CreateScope();
                CardscapeDbContext assertDb = assertScope.ServiceProvider
                    .GetRequiredService<CardscapeDbContext>();

                User pastGraceAfter = await Reload(assertDb, pastGraceId);
                User alreadyAnonymisedAfter = await Reload(assertDb, alreadyAnonymisedId);
                User withinGraceAfter = await Reload(assertDb, withinGraceId);
                User notDeletedAfter = await Reload(assertDb, notDeletedId);

                pastGraceAfter.IsAnonymised.Should().BeTrue(
                    "a soft-deleted user past the 30-day grace period should be anonymised");
                pastGraceAfter.AnonymisedAt.Should().NotBeNull();
                pastGraceAfter.AnonymisedAt!.Value.Should().Be(Now,
                    "the sweeper should stamp AnonymisedAt with the clock value");

                alreadyAnonymisedAfter.IsAnonymised.Should().BeTrue(
                    "an already-anonymised user stays anonymised");

                withinGraceAfter.IsAnonymised.Should().BeFalse(
                    "a user whose soft-delete is still within the 30-day grace period must not be anonymised");

                notDeletedAfter.IsAnonymised.Should().BeFalse(
                    "a user that was never soft-deleted must not be anonymised");
            }
            finally
            {
                await sp.DisposeAsync();
            }
        }
        finally
        {
            TryDelete(dbPath);
        }
    }

    [Fact]
    public async Task SweepOnceAsync_DoesNotThrowOnEmptyDatabase()
    {
        // The first production symptom was the sweeper
        // crashing every tick, including ticks where
        // there was nothing to do. This test pins the
        // "nothing to do" path: empty database, sweep
        // completes without exception, no rows mutated.
        string dbPath = Path.Combine(
            Path.GetTempPath(), $"retention-empty-{Guid.NewGuid():N}.db");
        try
        {
            ServiceProvider sp = BuildProvider(dbPath);
            try
            {
                using (IServiceScope seedScope = sp.CreateScope())
                {
                    CardscapeDbContext db = seedScope.ServiceProvider
                        .GetRequiredService<CardscapeDbContext>();
                    db.Database.Migrate();
                }

                var clock = new FakeClock(Now);
                var settings = Options.Create(new RetentionSettingsOptions
                {
                    UserGracePeriodDays = 30,
                    BatchSize = 100
                });
                var sweeper = new RetentionSweeper(
                    sp, clock, settings, NullLogger<RetentionSweeper>.Instance);

                Func<Task> sweep = () => sweeper.SweepOnceAsync(
                    TestContext.Current.CancellationToken);
                await sweep.Should().NotThrowAsync();

                using IServiceScope assertScope = sp.CreateScope();
                CardscapeDbContext assertDb = assertScope.ServiceProvider
                    .GetRequiredService<CardscapeDbContext>();
                (await assertDb.Users.CountAsync(TestContext.Current.CancellationToken))
                    .Should().Be(0);
            }
            finally
            {
                await sp.DisposeAsync();
            }
        }
        finally
        {
            TryDelete(dbPath);
        }
    }

    // ── helpers ────────────────────────────────────────────────

    private static ServiceProvider BuildProvider(string dbPath)
    {
        ServiceCollection services = new();
        services.AddDbContext<CardscapeDbContext>(opts =>
            opts.UseSqlite(
                $"Data Source={dbPath}",
                b => b.MigrationsAssembly("Cardscape.Infrastructure")));
        return services.BuildServiceProvider();
    }

    private static User SeedUser(
        DateTimeOffset now,
        int? deletedDaysAgo,
        bool isDeleted,
        bool isAnonymised)
    {
        // Unique email + display name per seed so the
        // unique index on `Email` never fires and the
        // assertions can target the user by id.
        string suffix = Guid.NewGuid().ToString("N");
        Result<User> registered = User.Register(
            new UserId(Guid.NewGuid()),
            EmailAddress.Create($"retention-test-{suffix}@cardscape.local").Value,
            DisplayName.Create($"retention-test-{suffix}").Value,
            PasswordHash.FromHashed("v1." + suffix + "." + suffix).Value,
            now);
        User user = registered.Value;
        if (isDeleted)
        {
            user.SoftDelete(now.AddDays(-(deletedDaysAgo ?? 0)));
        }
        if (isAnonymised)
        {
            user.Anonymise(now);
        }
        return user;
    }

    private static async Task<User> Reload(CardscapeDbContext db, UserId id) =>
        (await db.Users.FirstAsync(u => u.Id == id, TestContext.Current.CancellationToken));

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort — the temp directory is scrubbed
            // by the OS eventually.
        }
    }
}
