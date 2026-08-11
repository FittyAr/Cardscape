using System.Text.Json;
using System.Text.Json.Nodes;
using Cardscape.Application.Idempotency;
using Cardscape.Domain.Members;
using Cardscape.Mcp.Idempotency;
using Cardscape.Tests.Common.Fakes;
using FluentAssertions;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Cardscape.UnitTests.Security;

public sealed class McpToolIdempotencyPolicyTests
{
    private const string ValidKey = "mcp-write-request-1234";

    [Fact]
    public void SerializeCanonicalRequest_EquivalentPropertyOrder_ReturnsSameJson()
    {
        Dictionary<string, JsonElement> first = Arguments(
            ("options", "{\"z\":2,\"a\":[{\"y\":true,\"x\":1}]}"),
            ("boardId", "\"board-1\""));
        Dictionary<string, JsonElement> second = Arguments(
            ("boardId", "\"board-1\""),
            ("options", "{\"a\":[{\"x\":1,\"y\":true}],\"z\":2}"));

        string firstJson = McpToolIdempotencyPolicy.SerializeCanonicalRequest("boards_create", first);
        string secondJson = McpToolIdempotencyPolicy.SerializeCanonicalRequest("boards_create", second);

        secondJson.Should().Be(firstJson);
    }

    [Fact]
    public async Task InvokeAsync_WriteReplayWithSameRequest_ReturnsStoredResultWithoutInvokingNext()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var currentUser = AuthenticatedUser(Guid.NewGuid());
        JsonObject meta = Meta(ValidKey);
        Dictionary<string, JsonElement> arguments = Arguments(("name", "\"Roadmap\""));

        ResultDto first = await InvokeWrite(
            "boards_create", arguments, meta, currentUser, store,
            () => ValueTask.FromResult(new ResultDto(1, "first")));
        var calls = 0;
        ResultDto replay = await InvokeWrite(
            "boards_create", arguments, meta, currentUser, store,
            () =>
            {
                calls++;
                return ValueTask.FromResult(new ResultDto(2, "duplicate"));
            });

        replay.Should().Be(first);
        calls.Should().Be(0);
        store.Count.Should().Be(1);
    }

    [Fact]
    public async Task InvokeAsync_WriteReplay_PreservesMcpCallToolResultContent()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var currentUser = AuthenticatedUser(Guid.NewGuid());
        JsonObject meta = Meta(ValidKey);
        Dictionary<string, JsonElement> arguments = Arguments(("name", "\"Roadmap\""));
        var original = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "created" }]
        };
        await McpToolIdempotencyPolicy.InvokeAsync(
            "boards_create", arguments, meta, currentUser, store, new FakeClock(),
            () => ValueTask.FromResult(original), CancellationToken.None);

        CallToolResult replay = await McpToolIdempotencyPolicy.InvokeAsync(
            "boards_create", arguments, meta, currentUser, store, new FakeClock(),
            () => ValueTask.FromResult(new CallToolResult
            {
                Content = [new TextContentBlock { Text = "duplicate" }]
            }), CancellationToken.None);

        replay.Content.Should().ContainSingle()
            .Which.Should().BeOfType<TextContentBlock>()
            .Which.Text.Should().Be("created");
    }

    [Fact]
    public async Task InvokeAsync_SameKeyWithDifferentArguments_ThrowsConflict()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var currentUser = AuthenticatedUser(Guid.NewGuid());
        JsonObject meta = Meta(ValidKey);
        await InvokeWrite(
            "boards_create", Arguments(("name", "\"Roadmap\"")), meta, currentUser, store,
            () => ValueTask.FromResult(new ResultDto(1, "first")));

        Func<Task> act = async () => await InvokeWrite(
            "boards_create", Arguments(("name", "\"Operations\"")), meta, currentUser, store,
            () => ValueTask.FromResult(new ResultDto(2, "different")));

        await act.Should().ThrowAsync<IdempotencyKeyConflictException>();
        store.Count.Should().Be(1);
    }

    [Fact]
    public async Task InvokeAsync_SameKeyAndArgumentsForDifferentWriteTool_ThrowsConflict()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var currentUser = AuthenticatedUser(Guid.NewGuid());
        JsonObject meta = Meta(ValidKey);
        Dictionary<string, JsonElement> arguments = Arguments(("boardId", "\"board-1\""));
        await InvokeWrite(
            "boards_star", arguments, meta, currentUser, store,
            () => ValueTask.FromResult(new ResultDto(1, "starred")));

        Func<Task> act = async () => await InvokeWrite(
            "boards_unstar", arguments, meta, currentUser, store,
            () => ValueTask.FromResult(new ResultDto(2, "unstarred")));

        await act.Should().ThrowAsync<IdempotencyKeyConflictException>();
    }

    [Fact]
    public async Task InvokeAsync_SameKeyFromDifferentOwners_ExecutesBothRequests()
    {
        var store = new InMemoryIdempotencyKeyStore();
        JsonObject meta = Meta(ValidKey);
        Dictionary<string, JsonElement> arguments = Arguments(("boardId", "\"board-1\""));
        await InvokeWrite(
            "boards_star", arguments, meta, AuthenticatedUser(Guid.NewGuid()), store,
            () => ValueTask.FromResult(new ResultDto(1, "alice")));

        var calls = 0;
        ResultDto second = await InvokeWrite(
            "boards_star", arguments, meta, AuthenticatedUser(Guid.NewGuid()), store,
            () =>
            {
                calls++;
                return ValueTask.FromResult(new ResultDto(2, "bob"));
            });

        second.Should().Be(new ResultDto(2, "bob"));
        calls.Should().Be(1);
        store.Count.Should().Be(2);
    }

    [Fact]
    public async Task InvokeAsync_ReadToolWithKey_BypassesIdempotency()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var calls = 0;

        ResultDto result = await McpToolIdempotencyPolicy.InvokeAsync(
            "boards_get",
            Arguments(("boardId", "\"board-1\"")),
            Meta(ValidKey),
            AuthenticatedUser(Guid.NewGuid()),
            store,
            new FakeClock(),
            () =>
            {
                calls++;
                return ValueTask.FromResult(new ResultDto(1, "read"));
            },
            CancellationToken.None);

        result.Should().Be(new ResultDto(1, "read"));
        calls.Should().Be(1);
        store.Count.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_WriteWithoutMetaKey_BypassesIdempotency()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var calls = 0;

        ResultDto result = await McpToolIdempotencyPolicy.InvokeAsync(
            "boards_create",
            Arguments(("name", "\"Roadmap\"")),
            meta: null,
            AuthenticatedUser(Guid.NewGuid()),
            store,
            new FakeClock(),
            () =>
            {
                calls++;
                return ValueTask.FromResult(new ResultDto(1, "created"));
            },
            CancellationToken.None);

        result.Should().Be(new ResultDto(1, "created"));
        calls.Should().Be(1);
        store.Count.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_WriteWithNonStringMetaKey_RejectsBeforeInvokingNext()
    {
        var calls = 0;
        Func<Task> act = async () => await McpToolIdempotencyPolicy.InvokeAsync(
            "boards_create",
            Arguments(("name", "\"Roadmap\"")),
            new JsonObject { [McpToolIdempotencyPolicy.MetaPropertyName] = 42 },
            AuthenticatedUser(Guid.NewGuid()),
            new InMemoryIdempotencyKeyStore(),
            new FakeClock(),
            () =>
            {
                calls++;
                return ValueTask.FromResult(new ResultDto(1, "unexpected"));
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<McpException>()
            .WithMessage($"{McpToolIdempotencyPolicy.InvalidKeyErrorCode}*");
        calls.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_WriteWithInvalidStringKey_RejectsBeforeInvokingNext()
    {
        var calls = 0;
        Func<Task> act = async () => await InvokeWrite(
            "boards_create",
            Arguments(("name", "\"Roadmap\"")),
            Meta("short"),
            AuthenticatedUser(Guid.NewGuid()),
            new InMemoryIdempotencyKeyStore(),
            () =>
            {
                calls++;
                return ValueTask.FromResult(new ResultDto(1, "unexpected"));
            });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("idempotency.key.length*");
        calls.Should().Be(0);
    }

    private static ValueTask<ResultDto> InvokeWrite(
        string toolName,
        IDictionary<string, JsonElement> arguments,
        JsonObject meta,
        FakeCurrentUser currentUser,
        InMemoryIdempotencyKeyStore store,
        Func<ValueTask<ResultDto>> next) =>
        McpToolIdempotencyPolicy.InvokeAsync(
            toolName,
            arguments,
            meta,
            currentUser,
            store,
            new FakeClock(),
            next,
            CancellationToken.None);

    private static FakeCurrentUser AuthenticatedUser(Guid id) => new()
    {
        IsAuthenticated = true,
        Id = new UserId(id)
    };

    private static JsonObject Meta(string key) => new()
    {
        [McpToolIdempotencyPolicy.MetaPropertyName] = key
    };

    private static Dictionary<string, JsonElement> Arguments(
        params (string Name, string Json)[] values) => values.ToDictionary(
            value => value.Name,
            value => JsonDocument.Parse(value.Json).RootElement.Clone(),
            StringComparer.Ordinal);

    private sealed record ResultDto(int Id, string Name);
}
