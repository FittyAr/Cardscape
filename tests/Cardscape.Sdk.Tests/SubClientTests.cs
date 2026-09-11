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
                    visibility = "private"
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
    public async Task Boards_Rename_Async_Posts_Only_The_Canonical_Name_Field()
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
                    name = "Renamed",
                    description = (string?)null,
                    visibility = "private"
                })
            };
        });
        using HttpClient http = new(handler) { BaseAddress = new("https://api.example.test/") };
        await using CardscapeClient client = new(http, new CardscapeClientOptions
        {
            BaseAddress = new("https://api.example.test/")
        });

        Guid boardId = Guid.NewGuid();
        await client.Boards.RenameAsync(boardId, "Renamed", TestContext.Current.CancellationToken);

        capture.Method.Should().Be(HttpMethod.Post);
        capture.Path.Should().Be($"/api/boards/{boardId}/rename");
        JsonElement body = JsonDocument.Parse(capture.Body).RootElement;
        body.GetProperty("name").GetString().Should().Be("Renamed");
        body.TryGetProperty("newName", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Boards_Create_Async_Serializes_Visibility_As_A_Canonical_Name()
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
                    visibility = "workspace"
                })
            };
        });
        using HttpClient http = new(handler) { BaseAddress = new("https://api.example.test/") };
        await using CardscapeClient client = new(http, new CardscapeClientOptions
        {
            BaseAddress = new("https://api.example.test/")
        });

        await client.Boards.CreateAsync(
            new CreateBoardRequest(Guid.NewGuid(), "Board", Visibility: BoardVisibility.Workspace),
            TestContext.Current.CancellationToken);

        JsonElement body = JsonDocument.Parse(capture.Body).RootElement;
        body.GetProperty("visibility").ValueKind.Should().Be(JsonValueKind.String);
        body.GetProperty("visibility").GetString().Should().Be("workspace");
    }

    [Fact]
    public async Task Boards_Export_Async_Returns_Readable_Stream_That_Owns_Response()
    {
        byte[] archive = [0x50, 0x4B, 0x03, 0x04];
        TrackingByteArrayContent content = new(archive);
        using HttpMessageHandlerStub handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content
        });
        using HttpClient http = new(handler) { BaseAddress = new("https://api.example.test/") };
        await using CardscapeClient client = new(http, new CardscapeClientOptions
        {
            BaseAddress = new("https://api.example.test/")
        });

        Stream export = await client.Boards.ExportAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        using MemoryStream buffer = new();
        await export.CopyToAsync(buffer, TestContext.Current.CancellationToken);

        buffer.ToArray().Should().Equal(archive);
        content.IsDisposed.Should().BeFalse();

        await export.DisposeAsync();

        content.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task Boards_Export_Async_Disposes_NonSuccess_Response()
    {
        TrackingByteArrayContent content = new([0x7B, 0x7D]);
        using HttpMessageHandlerStub handler = new(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = content
        });
        using HttpClient http = new(handler) { BaseAddress = new("https://api.example.test/") };
        await using CardscapeClient client = new(http, new CardscapeClientOptions
        {
            BaseAddress = new("https://api.example.test/")
        });

        Func<Task> act = async () =>
            await client.Boards.ExportAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<HttpRequestException>();
        content.IsDisposed.Should().BeTrue();
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
            new MoveCardRequest(ListId: newListId, Position: 2.5),
            TestContext.Current.CancellationToken);

        capture.Method.Should().Be(HttpMethod.Post);
        capture.Path.Should().Be($"/api/cards/{cardId}/move");

        JsonElement body = JsonDocument.Parse(capture.Body).RootElement;
        body.GetProperty("listId").GetGuid().Should().Be(newListId);
        body.GetProperty("position").GetDouble().Should().Be(2.5);
        body.TryGetProperty("newListId", out _).Should().BeFalse();
        body.TryGetProperty("newPosition", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Cards_List_Async_Uses_The_Canonical_Board_Query()
    {
        RequestCapture capture = new();
        using HttpMessageHandlerStub handler = new(req =>
        {
            capture.CaptureSync(req);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Array.Empty<CardSummaryDto>())
            };
        });
        using HttpClient http = new(handler) { BaseAddress = new("https://api.example.test/") };
        await using CardscapeClient client = new(http, new CardscapeClientOptions
        {
            BaseAddress = new("https://api.example.test/")
        });

        Guid boardId = Guid.NewGuid();
        IReadOnlyList<CardSummaryDto> cards = await client.Cards.ListAsync(
            boardId, TestContext.Current.CancellationToken);

        cards.Should().BeEmpty();
        capture.Method.Should().Be(HttpMethod.Get);
        capture.Path.Should().Be("/api/cards");
        capture.Query.Should().Be($"?boardId={boardId}");
    }

    [Fact]
    public async Task Cards_Assign_And_AttachLabel_Put_Identifiers_In_The_Route()
    {
        List<string> paths = [];
        using HttpMessageHandlerStub handler = new(req =>
        {
            paths.Add(req.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { })
            };
        });
        using HttpClient http = new(handler) { BaseAddress = new("https://api.example.test/") };
        await using CardscapeClient client = new(http, new CardscapeClientOptions
        {
            BaseAddress = new("https://api.example.test/")
        });
        Guid cardId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Guid labelId = Guid.NewGuid();

        await client.Cards.AssignAsync(cardId, userId, TestContext.Current.CancellationToken);
        await client.Cards.AttachLabelAsync(cardId, labelId, TestContext.Current.CancellationToken);

        paths.Should().Equal(
            $"/api/cards/{cardId}/assign/{userId}",
            $"/api/cards/{cardId}/labels/{labelId}");
    }

    [Fact]
    public async Task Labels_Create_Async_Uses_The_Board_Scoped_Route()
    {
        RequestCapture capture = new();
        using HttpMessageHandlerStub handler = new(req =>
        {
            capture.CaptureSync(req);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = Guid.NewGuid(), boardId = Guid.NewGuid(), name = "Bug", color = "red" })
            };
        });
        using HttpClient http = new(handler) { BaseAddress = new("https://api.example.test/") };
        await using CardscapeClient client = new(http, new CardscapeClientOptions
        {
            BaseAddress = new("https://api.example.test/")
        });
        Guid boardId = Guid.NewGuid();

        await client.Labels.CreateAsync(
            new CreateLabelRequest(boardId, "Bug", "red"),
            TestContext.Current.CancellationToken);

        capture.Path.Should().Be($"/api/boards/{boardId}/labels");
        JsonElement body = JsonDocument.Parse(capture.Body).RootElement;
        body.TryGetProperty("boardId", out _).Should().BeFalse();
        body.GetProperty("name").GetString().Should().Be("Bug");
        body.GetProperty("color").GetString().Should().Be("red");
    }

    [Fact]
    public async Task Activities_ListForBoard_Deserializes_The_Cursor_Page()
    {
        Guid activityId = Guid.NewGuid();
        Guid boardId = Guid.NewGuid();
        using HttpMessageHandlerStub handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                items = new[]
                {
                    new
                    {
                        id = activityId,
                        boardId,
                        cardId = (Guid?)null,
                        actorId = Guid.NewGuid(),
                        actorDisplayName = "Ada",
                        kind = "boardCreated",
                        payloadJson = "{}",
                        occurredAt = DateTimeOffset.UtcNow
                    }
                },
                nextCursor = "cursor-2"
            })
        });
        using HttpClient http = new(handler) { BaseAddress = new("https://api.example.test/") };
        await using CardscapeClient client = new(http, new CardscapeClientOptions
        {
            BaseAddress = new("https://api.example.test/")
        });

        ActivityPageDto page = await client.Activities.ListForBoardAsync(
            boardId, ct: TestContext.Current.CancellationToken);

        page.NextCursor.Should().Be("cursor-2");
        page.Items.Should().ContainSingle().Which.Id.Should().Be(activityId);
        page.Items[0].Kind.Should().Be(ActivityKind.BoardCreated);
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
                    region = "europe"
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
        body.GetProperty("region").ValueKind.Should().Be(JsonValueKind.String);
        body.GetProperty("region").GetString().Should().Be("europe");
    }

    private sealed class RequestCapture
    {
        public HttpMethod Method { get; private set; } = HttpMethod.Get;
        public string Path { get; private set; } = string.Empty;
        public string Query { get; private set; } = string.Empty;
        public string Body { get; private set; } = string.Empty;

        public void CaptureSync(HttpRequestMessage request)
        {
            Method = request.Method;
            Path = request.RequestUri?.AbsolutePath ?? string.Empty;
            Query = request.RequestUri?.Query ?? string.Empty;
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

    private sealed class TrackingByteArrayContent(byte[] content) : ByteArrayContent(content)
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
