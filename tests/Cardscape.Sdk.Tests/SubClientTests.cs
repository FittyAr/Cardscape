using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cardscape.Sdk;
using FluentAssertions;
using Xunit;

namespace Cardscape.Sdk.Tests;

/// <summary>
/// Wire-format tests for the SDK's per-resource sub-clients.
/// The handler is an in-process stub so the assertions are
/// about the URL + body the SDK emits, not about the network
/// round-trip.
/// </summary>
public sealed class SubClientTests
{
    [Fact]
    public async Task Boards_Get_Async_Hits_The_Expected_Path()
    {
        RequestCapture capture = new();
        using HttpMessageHandlerStub handler = new(req =>
        {
            capture.CaptureSync(req);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    id = Guid.NewGuid(),
                    name = "Board",
                    description = (string?)null,
                    visibility = 0
                })
            };
        });
        using HttpClient http = new(handler) { BaseAddress = new("https://api.example.test/") };
        await using CardscapeClient client = new(http, new CardscapeClientOptions
        {
            BaseAddress = new("https://api.example.test/")
        });

        Guid boardId = Guid.NewGuid();
        BoardDto board = await client.Boards.GetAsync(boardId, TestContext.Current.CancellationToken);

        board.Should().NotBeNull();
        capture.Method.Should().Be(HttpMethod.Get);
        capture.Path.Should().Be($"/api/boards/{boardId}");
    }

    [Fact]
    public async Task Cards_Move_Async_Posts_The_Expected_Body()
    {
        RequestCapture capture = new();
        using HttpMessageHandlerStub handler = new(req =>
        {
            capture.CaptureSync(req);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    id = Guid.NewGuid(),
                    title = "Card",
                    listId = Guid.NewGuid(),
                    position = 1.0
                })
            };
        });
        using HttpClient http = new(handler) { BaseAddress = new("https://api.example.test/") };
        await using CardscapeClient client = new(http, new CardscapeClientOptions
        {
            BaseAddress = new("https://api.example.test/")
        });

        Guid cardId = Guid.NewGuid();
        Guid newListId = Guid.NewGuid();
        await client.Cards.MoveAsync(
            cardId,
            new MoveCardRequest(NewListId: newListId, NewPosition: 2.5),
            TestContext.Current.CancellationToken);

        capture.Method.Should().Be(HttpMethod.Post);
        capture.Path.Should().Be($"/api/cards/{cardId}/move");

        JsonElement body = JsonDocument.Parse(capture.Body).RootElement;
        body.GetProperty("newListId").GetGuid().Should().Be(newListId);
        body.GetProperty("newPosition").GetDouble().Should().Be(2.5);
    }

    [Fact]
    public async Task Lists_Create_Async_Posts_Name()
    {
        RequestCapture capture = new();
        using HttpMessageHandlerStub handler = new(req =>
        {
            capture.CaptureSync(req);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    id = Guid.NewGuid(),
                    boardId = Guid.NewGuid(),
                    name = "To Do",
                    position = 1.0
                })
            };
        });
        using HttpClient http = new(handler) { BaseAddress = new("https://api.example.test/") };
        await using CardscapeClient client = new(http, new CardscapeClientOptions
        {
            BaseAddress = new("https://api.example.test/")
        });

        Guid boardId = Guid.NewGuid();
        await client.Lists.CreateAsync(
            new CreateListRequest(boardId, "Backlog"),
            TestContext.Current.CancellationToken);

        capture.Method.Should().Be(HttpMethod.Post);
        capture.Path.Should().Be("/api/lists/");
        Assert.True(capture.Body is { Length: > 0 },
            $"body should be non-empty; actual: '{capture.Body ?? "<null>"}'");
        using JsonDocument doc = JsonDocument.Parse(capture.Body!);
        JsonElement body = doc.RootElement.Clone();
        bool hasName = body.TryGetProperty("name", out JsonElement nameElement);
        Assert.True(hasName, $"body has no 'name' property; body: {capture.Body}");
        string nameStr = nameElement.GetString() ?? string.Empty;
        Assert.Equal("Backlog", nameStr);
    }

    [Fact]
    public async Task Boards_Star_Async_Hits_The_Star_Endpoint()
    {
        RequestCapture capture = new();
        using HttpMessageHandlerStub handler = new(req =>
        {
            capture.CaptureSync(req);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = Guid.NewGuid(), isStarred = true })
            };
        });
        using HttpClient http = new(handler) { BaseAddress = new("https://api.example.test/") };
        await using CardscapeClient client = new(http, new CardscapeClientOptions
        {
            BaseAddress = new("https://api.example.test/")
        });

        Guid boardId = Guid.NewGuid();
        await client.Boards.StarAsync(boardId, TestContext.Current.CancellationToken);

        capture.Method.Should().Be(HttpMethod.Post);
        capture.Path.Should().Be($"/api/boards/{boardId}/star");
    }

    [Fact]
    public async Task Cards_Create_Async_Posts_Title_And_Description()
    {
        RequestCapture capture = new();
        using HttpMessageHandlerStub handler = new(req =>
        {
            capture.CaptureSync(req);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = Guid.NewGuid(), title = "Hello" })
            };
        });
        using HttpClient http = new(handler) { BaseAddress = new("https://api.example.test/") };
        await using CardscapeClient client = new(http, new CardscapeClientOptions
        {
            BaseAddress = new("https://api.example.test/")
        });

        Guid listId = Guid.NewGuid();
        await client.Cards.CreateAsync(
            new CreateCardRequest(listId, "Hello", "world"),
            TestContext.Current.CancellationToken);

        capture.Method.Should().Be(HttpMethod.Post);
        capture.Path.Should().Be("/api/cards/");
        JsonElement body = JsonDocument.Parse(capture.Body).RootElement;
        body.GetProperty("listId").GetGuid().Should().Be(listId);
        body.GetProperty("title").GetString().Should().Be("Hello");
        body.GetProperty("description").GetString().Should().Be("world");
    }

    [Fact]
    public async Task Workspaces_Create_Async_Posts_Name_And_Region()
    {
        RequestCapture capture = new();
        using HttpMessageHandlerStub handler = new(req =>
        {
            capture.CaptureSync(req);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    id = Guid.NewGuid(),
                    name = "WS",
                    region = (int)Region.Europe
                })
            };
        });
        using HttpClient http = new(handler) { BaseAddress = new("https://api.example.test/") };
        await using CardscapeClient client = new(http, new CardscapeClientOptions
        {
            BaseAddress = new("https://api.example.test/")
        });

        await client.Workspaces.CreateAsync(
            new CreateWorkspaceRequest("WS", Region.Europe),
            TestContext.Current.CancellationToken);

        capture.Method.Should().Be(HttpMethod.Post);
        capture.Path.Should().Be("/api/workspaces/");
        JsonElement body = JsonDocument.Parse(capture.Body).RootElement;
        body.GetProperty("name").GetString().Should().Be("WS");
        body.GetProperty("region").GetInt32().Should().Be((int)Region.Europe);
    }

    private sealed class RequestCapture
    {
        public HttpMethod Method { get; private set; } = HttpMethod.Get;
        public string Path { get; private set; } = string.Empty;
        public string Body { get; private set; } = string.Empty;

        public void CaptureSync(HttpRequestMessage request)
        {
            Method = request.Method;
            Path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (request.Content is not null)
            {
                // The HttpClient materialises the content before
                // handing the request to the handler pipeline.
                // We block on the buffer so the body is fully
                // available when we read it.
                request.Content.LoadIntoBufferAsync().GetAwaiter().GetResult();
                Body = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
        }
    }
}
