using System.Text.Json;
using Cardscape.Application.Idempotency;
using Cardscape.Domain.Members;
using Cardscape.Tests.Common.Fakes;
using FluentAssertions;

namespace Cardscape.UnitTests.Application.Idempotency;

public class IdempotencyKeyMiddlewareTests
{
    private const string ValidKey = "valid-idempotency-key-123";

    [Fact]
    public async Task ExecuteAsync_NullKey_RunsHandlerWithoutStoring()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var clock = new FakeClock();
        var user = await SeedUserAsync();
        var current = FakeCurrentUser.AuthenticatedAs(user);

        var calls = 0;
        var result = await IdempotencyKeyMiddleware.ExecuteAsync(
            idempotencyKey: null,
            requestJson: "{\"x\":1}",
            currentUser: current,
            store: store,
            clock: clock,
            handler: () =>
            {
                calls++;
                return Task.FromResult("handler-result");
            },
            ct: CancellationToken.None);

        result.Should().Be("handler-result");
        calls.Should().Be(1);
        store.Count.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyKey_RunsHandlerWithoutStoring()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var clock = new FakeClock();
        var user = await SeedUserAsync();
        var current = FakeCurrentUser.AuthenticatedAs(user);

        var calls = 0;
        var result = await IdempotencyKeyMiddleware.ExecuteAsync(
            idempotencyKey: "   ",
            requestJson: "{\"x\":1}",
            currentUser: current,
            store: store,
            clock: clock,
            handler: () =>
            {
                calls++;
                return Task.FromResult("handler-result");
            },
            ct: CancellationToken.None);

        result.Should().Be("handler-result");
        calls.Should().Be(1);
        store.Count.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_AnonymousCurrentUser_Throws()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var clock = new FakeClock();

        var act = async () => await IdempotencyKeyMiddleware.ExecuteAsync(
            idempotencyKey: ValidKey,
            requestJson: "{\"x\":1}",
            currentUser: FakeCurrentUser.Anonymous(),
            store: store,
            clock: clock,
            handler: () => Task.FromResult("x"),
            ct: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no authenticated principal*");
        store.Count.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_KeyTooShort_Throws()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var clock = new FakeClock();
        var user = await SeedUserAsync();

        var act = async () => await IdempotencyKeyMiddleware.ExecuteAsync(
            idempotencyKey: "short",
            requestJson: "{\"x\":1}",
            currentUser: FakeCurrentUser.AuthenticatedAs(user),
            store: store,
            clock: clock,
            handler: () => Task.FromResult("x"),
            ct: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("idempotency.key.length*");
        store.Count.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_FirstCall_RunsHandlerAndPersistsRecord()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var clock = new FakeClock();
        var user = await SeedUserAsync();
        var current = FakeCurrentUser.AuthenticatedAs(user);

        var calls = 0;
        var result = await IdempotencyKeyMiddleware.ExecuteAsync(
            idempotencyKey: ValidKey,
            requestJson: "{\"a\":1}",
            currentUser: current,
            store: store,
            clock: clock,
            handler: () =>
            {
                calls++;
                return Task.FromResult(new HandlerDto(1, "alpha"));
            },
            ct: CancellationToken.None);

        result.Id.Should().Be(1);
        result.Name.Should().Be("alpha");
        calls.Should().Be(1);
        store.Count.Should().Be(1);

        var stored = store.All.Single();
        stored.OwnerId.Should().Be(user.Id);
        stored.Key.Value.Should().Be(ValidKey);
        stored.RequestHash.Should().Be(RequestHashFor("{\"a\":1}"));
        stored.ResponseStatusCode.Should().Be(200);
    }

    [Fact]
    public async Task ExecuteAsync_ReplayWithSamePayload_ShortCircuitsAndDoesNotRerunHandler()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var clock = new FakeClock();
        var user = await SeedUserAsync();
        var current = FakeCurrentUser.AuthenticatedAs(user);
        var payload = "{\"a\":1}";

        var first = await IdempotencyKeyMiddleware.ExecuteAsync<HandlerDto>(
            idempotencyKey: ValidKey,
            requestJson: payload,
            currentUser: current,
            store: store,
            clock: clock,
            handler: () => Task.FromResult(new HandlerDto(1, "alpha")),
            ct: CancellationToken.None);

        var calls = 0;
        var second = await IdempotencyKeyMiddleware.ExecuteAsync<HandlerDto>(
            idempotencyKey: ValidKey,
            requestJson: payload,
            currentUser: current,
            store: store,
            clock: clock,
            handler: () =>
            {
                calls++;
                // If the middleware truly short-circuits this is never called.
                return Task.FromResult(new HandlerDto(99, "different"));
            },
            ct: CancellationToken.None);

        second.Should().BeEquivalentTo(first);
        calls.Should().Be(0);
        store.Count.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ReplayWithDifferentPayload_ThrowsConflict()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var clock = new FakeClock();
        var user = await SeedUserAsync();
        var current = FakeCurrentUser.AuthenticatedAs(user);

        await IdempotencyKeyMiddleware.ExecuteAsync<HandlerDto>(
            idempotencyKey: ValidKey,
            requestJson: "{\"a\":1}",
            currentUser: current,
            store: store,
            clock: clock,
            handler: () => Task.FromResult(new HandlerDto(1, "alpha")),
            ct: CancellationToken.None);

        var act = async () => await IdempotencyKeyMiddleware.ExecuteAsync<HandlerDto>(
            idempotencyKey: ValidKey,
            requestJson: "{\"a\":2}",
            currentUser: current,
            store: store,
            clock: clock,
            handler: () => Task.FromResult(new HandlerDto(2, "beta")),
            ct: CancellationToken.None);

        var assertion = await act.Should().ThrowAsync<IdempotencyKeyConflictException>();
        assertion.Which.Existing.Key.Value.Should().Be(ValidKey);
        store.Count.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ReplayFromDifferentOwner_StillRunsHandler()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var clock = new FakeClock();
        var alice = await SeedUserAsync("alice@example.com");
        var bob = await SeedUserAsync("bob@example.com");

        await IdempotencyKeyMiddleware.ExecuteAsync<HandlerDto>(
            idempotencyKey: ValidKey,
            requestJson: "{\"a\":1}",
            currentUser: FakeCurrentUser.AuthenticatedAs(alice),
            store: store,
            clock: clock,
            handler: () => Task.FromResult(new HandlerDto(1, "alpha")),
            ct: CancellationToken.None);

        var calls = 0;
        var second = await IdempotencyKeyMiddleware.ExecuteAsync<HandlerDto>(
            idempotencyKey: ValidKey,
            requestJson: "{\"a\":1}",
            currentUser: FakeCurrentUser.AuthenticatedAs(bob),
            store: store,
            clock: clock,
            handler: () =>
            {
                calls++;
                return Task.FromResult(new HandlerDto(7, "bob"));
            },
            ct: CancellationToken.None);

        second.Id.Should().Be(7);
        calls.Should().Be(1);
        store.Count.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_StringPayload_ReplayDoesNotReinvokeHandler()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var clock = new FakeClock();
        var user = await SeedUserAsync();

        await IdempotencyKeyMiddleware.ExecuteAsync<string>(
            idempotencyKey: ValidKey,
            requestJson: "{}",
            currentUser: FakeCurrentUser.AuthenticatedAs(user),
            store: store,
            clock: clock,
            handler: () => Task.FromResult("hello world"),
            ct: CancellationToken.None);

        var calls = 0;
        var second = await IdempotencyKeyMiddleware.ExecuteAsync<string>(
            idempotencyKey: ValidKey,
            requestJson: "{}",
            currentUser: FakeCurrentUser.AuthenticatedAs(user),
            store: store,
            clock: clock,
            handler: () =>
            {
                calls++;
                return Task.FromResult("DIFFERENT");
            },
            ct: CancellationToken.None);

        // The middleware stored the JSON-encoded form of the
        // string and replays it verbatim on a hit. The contract
        // is that the handler is NOT re-invoked, regardless of
        // what the handler would have returned.
        calls.Should().Be(0);
        second.Should().NotBe("DIFFERENT");
        second.Should().NotBe("hello world");
        store.Count.Should().Be(1);
    }

    private static async Task<User> SeedUserAsync(string email = "alice@example.com")
    {
        var users = new InMemoryUserRepository();
        var clock = new FakeClock();
        var hash = new FakePasswordHasher().Hash("Passw0rd!");
        var user = User.Register(
            UserId.New(),
            EmailAddress.Create(email).Value,
            DisplayName.Create("Alice").Value,
            hash,
            clock.UtcNow).Value;
        await users.AddAsync(user);
        return user;
    }

    private static string RequestHashFor(string? json) =>
        Cardscape.Application.Abstractions.Idempotency.RequestHasher.Hash(json);

    public sealed record HandlerDto(int Id, string Name);
}
