using Cardscape.Application.Abstractions.Idempotency;
using Cardscape.Application.Idempotency;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Tests.Common.Fakes;
using FluentAssertions;

namespace Cardscape.UnitTests.Application.Idempotency;

public class IssueIdempotencyKeyCommandHandlerTests
{
    private const string ValidKey = "issue-cmd-key-1234";

    [Fact]
    public async Task Handle_AsAnonymous_ReturnsUnauthenticated()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var ctx = new HandlersTestContext();

        var result = await IssueIdempotencyKeyCommandHandler.Handle(
            new IssueIdempotencyKeyCommand(ValidKey, RequestHashFor("{}"), 200, "{}"),
            store, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthenticated);
        store.Count.Should().Be(0);
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithInvalidKey_ReturnsValidationFailure()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(user);

        var result = await IssueIdempotencyKeyCommandHandler.Handle(
            new IssueIdempotencyKeyCommand("short", RequestHashFor("{}"), 200, "{}"),
            store, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().StartWith("idempotency.key");
        store.Count.Should().Be(0);
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithBadHash_ReturnsValidationFailure()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(user);

        var result = await IssueIdempotencyKeyCommandHandler.Handle(
            new IssueIdempotencyKeyCommand(ValidKey, "not-a-valid-sha256", 200, "{}"),
            store, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("idempotency.key.hash_invalid");
        store.Count.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithValidInput_PersistsAndCommits()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(user);
        var hash = RequestHashFor("{\"a\":1}");

        var result = await IssueIdempotencyKeyCommandHandler.Handle(
            new IssueIdempotencyKeyCommand(ValidKey, hash, 201, "{\"ok\":true}"),
            store, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().NotBe(Guid.Empty);
        store.Count.Should().Be(1);
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(1);

        var stored = store.All.Single();
        stored.OwnerId.Should().Be(user.Id);
        stored.Key.Value.Should().Be(ValidKey);
        stored.RequestHash.Should().Be(hash);
        stored.ResponseStatusCode.Should().Be(201);
        stored.ResponseJson.Should().Be("{\"ok\":true}");
    }

    private static string RequestHashFor(string? json) =>
        RequestHasher.Hash(json);
}
