using Cardscape.Domain.Members;
using Cardscape.Tests.Common.Fakes;
using ProductionPendingTotpLoginStore = Cardscape.Infrastructure.Authentication.InMemoryPendingTotpLoginStore;

namespace Cardscape.UnitTests.Authentication;

public sealed class InMemoryPendingTotpLoginStoreTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Consume_BeforeExpiration_ReturnsUserOnlyOnce()
    {
        var clock = new FakeClock(Now);
        var store = new ProductionPendingTotpLoginStore(clock);
        var userId = new UserId(Guid.NewGuid());

        string token = store.Mint(userId);
        clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromTicks(1));

        store.Consume(token).Should().Be(userId);
        store.Consume(token).Should().BeNull(
            "pending TOTP challenges must be single-use");
    }

    [Fact]
    public void Consume_AtExpirationBoundary_ReturnsNullAndRemovesToken()
    {
        var clock = new FakeClock(Now);
        var store = new ProductionPendingTotpLoginStore(clock);
        var userId = new UserId(Guid.NewGuid());

        string token = store.Mint(userId);
        clock.Advance(TimeSpan.FromMinutes(5));

        store.Consume(token).Should().BeNull(
            "the five-minute lifetime is exclusive at its upper boundary");
        clock.UtcNow = Now;
        store.Consume(token).Should().BeNull(
            "an expired challenge must be removed even when consumption fails");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown-token")]
    public void Consume_WithInvalidToken_ReturnsNull(string? token)
    {
        var store = new ProductionPendingTotpLoginStore(new FakeClock(Now));

        store.Consume(token!).Should().BeNull();
    }
}
