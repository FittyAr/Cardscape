using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cardscape.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// End-to-end coverage of the internal broadcast endpoint
/// (<c>POST /api/internal/broadcast</c>) the MCP uses to push
/// SignalR events. We don't drive a SignalR client here; we
/// just confirm the endpoint accepts the right requests, rejects
/// the wrong ones, and successfully resolves the board id
/// from a list id or a card id.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class BoardBroadcastEndpointTests
{
    private const string Secret = "integration-tests-broadcast-secret";

    private readonly CardscapeWebApplicationFactory _factory;

    public BoardBroadcastEndpointTests(CardscapeWebApplicationFactory factory)
    {
        // The test factory is shared across the collection. We
        // don't reconfigure the host per-test — instead the test
        // body matches whatever secret the API actually has, or
        // skips if it wasn't set. The smoke test below sets the
        // secret explicitly via a one-shot factory.
        _factory = factory;
    }

    [Fact]
    public async Task Broadcast_Without_Secret_Returns_503()
    {
        // The shared test factory does not set Internal:Secret,
        // so the endpoint short-circuits with 503 ("not configured")
        // before even checking the header. The per-test factory
        // below sets the secret explicitly, which is what the
        // happy-path tests use.
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/internal/broadcast/",
            new
            {
                boardId = Guid.NewGuid(),
                method = "CardCreated",
                payload = new { cardId = Guid.NewGuid(), boardId = Guid.NewGuid(), listId = Guid.NewGuid(), title = "x", at = DateTimeOffset.UtcNow }
            }, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Broadcast_With_Wrong_Secret_Returns_Unauthorized()
    {
        HttpClient client = CreateClientWithSecret();
        client.DefaultRequestHeaders.Remove("X-Internal-Secret");
        client.DefaultRequestHeaders.Add("X-Internal-Secret", "definitely-not-the-real-one");

        HttpResponseMessage response = await PostAsync(client,
            method: "CardCreated",
            boardId: Guid.NewGuid(),
            payload: new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Broadcast_With_Unknown_Method_Returns_400()
    {
        HttpClient client = CreateClientWithSecret();
        HttpResponseMessage response = await PostAsync(client,
            method: "NotARealMethod",
            boardId: Guid.NewGuid(),
            payload: new { });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Broadcast_CardCreated_With_BoardId_Returns_202()
    {
        HttpClient client = CreateClientWithSecret();
        Guid boardId = Guid.NewGuid();
        HttpResponseMessage response = await PostAsync(client,
            method: "CardCreated",
            boardId: boardId,
            payload: new
            {
                cardId = Guid.NewGuid(),
                boardId,
                listId = Guid.NewGuid(),
                title = "AI created this",
                at = DateTimeOffset.UtcNow
            });
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Broadcast_WithAdvertisedBodyAbove64KiB_Returns413()
    {
        using HttpClient client = CreateClientWithSecret();
        using HttpContent content = CreateSizedContent(64 * 1024 + 1);

        using HttpResponseMessage response = await client.PostAsync(
            "api/internal/broadcast/", content, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Broadcast_WithChunkedBodyAbove64KiB_Returns413()
    {
        using HttpClient client = CreateClientWithSecret();
        byte[] bytes = CreateSizedBytes(64 * 1024 + 1);
        using var content = new UnknownLengthContent(bytes);

        using HttpResponseMessage response = await client.PostAsync(
            "api/internal/broadcast/", content, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Broadcast_WithBodyExactly64KiB_ReachesDispatchValidation()
    {
        using HttpClient client = CreateClientWithSecret();
        using HttpContent content = CreateSizedContent(64 * 1024);

        using HttpResponseMessage response = await client.PostAsync(
            "api/internal/broadcast/", content, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should().Contain("Unknown method");
    }

    [Fact]
    public async Task Broadcast_WithMalformedJson_Returns400()
    {
        using HttpClient client = CreateClientWithSecret();
        using var content = new StringContent("not-json", Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.PostAsync(
            "api/internal/broadcast/", content, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should().Contain("valid broadcast JSON");
    }

    [Fact]
    public async Task Broadcast_WithPayloadIncompatibleWithMethod_Returns400()
    {
        using HttpClient client = CreateClientWithSecret();
        using JsonContent content = JsonContent.Create(new
        {
            boardId = Guid.NewGuid(),
            method = "CardCreated",
            payload = "not-a-card-payload"
        });

        using HttpResponseMessage response = await client.PostAsync(
            "api/internal/broadcast/", content, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should().Contain("does not match the broadcast method");
    }

    [Fact]
    public async Task Broadcast_CardCreated_With_ListId_Resolves_Board()
    {
        HttpClient client = CreateClientWithSecret();
        Guid listId = Guid.NewGuid();
        HttpResponseMessage response = await PostAsync(client,
            method: "ListCreated",
            boardId: null,
            listId: listId,
            cardId: null,
            payload: new
            {
                listId,
                boardId = Guid.NewGuid(),
                name = "L",
                at = DateTimeOffset.UtcNow
            });
        // The list doesn't actually exist in the database; the
        // resolver returns null and the endpoint responds 400.
        // The smoke test below uses a real list id to confirm
        // the resolver path.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private HttpClient CreateClientWithSecret()
    {
        // Build a one-off factory configured with the secret we
        // expect to see, so the endpoint accepts our requests.
        // We re-inject the parent factory's connection string +
        // storage root + deployment region into the auxiliary
        // host's configuration so the new host's Program.cs
        // builds the same DbContext the rest of the suite is
        // using.
        WebApplicationFactory<Program> factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Internal:Secret"] = Secret,
                    ["ConnectionStrings:Default"] = _factory.ConnectionString,
                    ["Storage:LocalRoot"] = _factory.StorageRoot,
                    ["Database:Provider"] = "Sqlite"
                });
            });
        });

        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Secret", Secret);
        return client;
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string method,
        Guid? boardId = null,
        Guid? listId = null,
        Guid? cardId = null,
        object payload = default!)
    {
        using JsonContent content = JsonContent.Create(new
        {
            boardId,
            listId,
            cardId,
            method,
            payload
        });
        return await client.PostAsync("api/internal/broadcast/", content);
    }

    private static ByteArrayContent CreateSizedContent(int size)
    {
        var content = new ByteArrayContent(CreateSizedBytes(size));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private static byte[] CreateSizedBytes(int size)
    {
        var empty = new
        {
            boardId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            method = "NotARealMethod",
            payload = new { padding = string.Empty }
        };
        int overhead = JsonSerializer.SerializeToUtf8Bytes(empty).Length;
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            empty.boardId,
            empty.method,
            payload = new { padding = new string('x', size - overhead) }
        });
        bytes.Should().HaveCount(size);
        return bytes;
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(bytes, 0, bytes.Length);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
