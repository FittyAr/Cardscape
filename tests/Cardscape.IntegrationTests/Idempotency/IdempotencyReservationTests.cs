using Cardscape.Application.Idempotency;
using Cardscape.Domain.Idempotency;
using Cardscape.Domain.Members;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Infrastructure.Repositories;
using Cardscape.Tests.Common.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.IntegrationTests.Idempotency;

public sealed class IdempotencyReservationTests
{
    [Fact]
    public async Task ExecuteAsync_TwoDbContextsCompete_ExecutesEffectOnceAndPersistsWinner()
    {
        string databasePath = Path.Combine(
            Path.GetTempPath(), $"cardscape-idempotency-{Guid.NewGuid():N}.db");
        try
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            var options = new DbContextOptionsBuilder<CardscapeDbContext>()
                .UseSqlite($"Data Source={databasePath};Default Timeout=30")
                .Options;
            await using (var setup = new CardscapeDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync(ct);
            }

            await using var firstContext = new CardscapeDbContext(options);
            await using var secondContext = new CardscapeDbContext(options);
            var firstStore = new IdempotencyKeyRepository(firstContext);
            var secondStore = new IdempotencyKeyRepository(secondContext);
            var clock = new FakeClock();
            var current = new FakeCurrentUser
            {
                IsAuthenticated = true,
                Id = UserId.New()
            };
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var effects = 0;

            Task<ResultDto> first = IdempotencyKeyMiddleware.ExecuteAsync(
                "sqlite-concurrent-key", "{\"value\":1}", current, firstStore, clock,
                async () =>
                {
                    Interlocked.Increment(ref effects);
                    entered.SetResult();
                    await release.Task;
                    return new ResultDto(1, "winner");
                }, ct);
            await entered.Task;
            Task<ResultDto> second = IdempotencyKeyMiddleware.ExecuteAsync(
                "sqlite-concurrent-key", "{\"value\":1}", current, secondStore, clock,
                () =>
                {
                    Interlocked.Increment(ref effects);
                    return Task.FromResult(new ResultDto(2, "loser"));
                }, ct);

            release.SetResult();
            ResultDto[] results = await Task.WhenAll(first, second);

            effects.Should().Be(1);
            results.Should().AllBeEquivalentTo(new ResultDto(1, "winner"));
            await using var verification = new CardscapeDbContext(options);
            IdempotencyKey persisted = await verification.IdempotencyKeys
                .AsNoTracking()
                .SingleAsync(ct);
            persisted.IsPending.Should().BeFalse();
            persisted.ResponseStatusCode.Should().Be(200);
            persisted.ResponseJson.Should().Contain("winner");
            persisted.UpdatedAt.Should().Be(clock.UtcNow);
            persisted.RowVersion.Should().Be(1);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    private sealed record ResultDto(int Id, string Name);
}
