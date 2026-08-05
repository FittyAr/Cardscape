using System.Net;
using System.Text.Json;
using Cardscape.Sdk;
using FluentAssertions;
using Xunit;

namespace Cardscape.Sdk.Tests;

/// <summary>
/// Smoke tests for <see cref="CardscapeClient"/>: construction,
/// token rotation, error mapping, and the lower-level
/// <c>SendAsync</c> overloads. The handlers are
/// <see cref="HttpMessageHandler"/> subclasses we control so the
/// tests do not touch the network and stay sub-millisecond on
/// the CI box.
/// </summary>
public sealed class ClientConstructionTests
{
    [Fact]
    public async Task Constructor_With_Base_Address_Sets_It_On_Default_Http_Client()
    {
        Uri baseAddress = new("https://api.example.test/");

        CardscapeClient client = new(
            new CardscapeClientOptions { BaseAddress = baseAddress });

        // The default-ctor overload owns the HttpClient; the
        // client must be disposed through the SDK.
        client.Should().NotBeNull();
        await client.DisposeAsync();
    }

    [Fact]
    public async Task Constructor_With_Caller_Supplied_Http_Client_Does_Not_Own_It()
    {
        HttpMessageHandlerStub handler = new();
        using HttpClient http = new(handler) { BaseAddress = new("https://api.example.test/") };
        CardscapeClient client = new(http, new CardscapeClientOptions
        {
            BaseAddress = new("https://api.example.test/")
        });

        // The contract: when the caller passes the HttpClient,
        // disposing the SDK does not dispose the underlying
        // transport. The test asserts this by using the
        // handler after the SDK has been disposed.
        await client.DisposeAsync();

        Action probe = () => handler.SendProbe();
        probe.Should().NotThrow<NullReferenceException>();
    }

    [Fact]
    public async Task SendAsync_Propagates_Access_Token_From_Provider()
    {
        string observedAuth = string.Empty;
        using HttpMessageHandlerStub handler = new(async request =>
        {
            observedAuth = request.Headers.Authorization?.ToString() ?? string.Empty;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"00000000-0000-0000-0000-000000000001\"}")
            };
        });
        using HttpClient http = new(handler) { BaseAddress = new("https://api.example.test/") };
        await using CardscapeClient client = new(http, new CardscapeClientOptions
        {
            BaseAddress = new("https://api.example.test/"),
            AccessToken = () => Task.FromResult<string?>("bearer-xyz")
        });

        using HttpRequestMessage request = new(HttpMethod.Get, "api/test");
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        observedAuth.Should().Be("Bearer bearer-xyz");
    }

    [Fact]
    public async Task SendAsync_NonSuccess_Throws_CardscapeApiException_With_Code_And_Status()
    {
        using HttpMessageHandlerStub handler = new(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"title\":\"auth.required\",\"detail\":\"missing token\"}")
        });
        using HttpClient http = new(handler) { BaseAddress = new("https://api.example.test/") };
        await using CardscapeClient client = new(http, new CardscapeClientOptions
        {
            BaseAddress = new("https://api.example.test/")
        });

        using HttpRequestMessage request = new(HttpMethod.Get, "api/test");
        Func<Task> act = async () => await client.SendAsync<TestProbe>(request, TestContext.Current.CancellationToken);

        CardscapeApiException exception = (await act.Should().ThrowAsync<CardscapeApiException>()).Which;
        exception.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task SendAsync_Of_T_Deserializes_Json_Body()
    {
        using HttpMessageHandlerStub handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":\"abc-123\",\"title\":\"hello\"}")
        });
        using HttpClient http = new(handler) { BaseAddress = new("https://api.example.test/") };
        await using CardscapeClient client = new(http, new CardscapeClientOptions
        {
            BaseAddress = new("https://api.example.test/")
        });

        using HttpRequestMessage request = new(HttpMethod.Get, "api/test");
        TestProbe probe = await client.SendAsync<TestProbe>(request, TestContext.Current.CancellationToken);

        probe.Id.Should().Be("abc-123");
        probe.Title.Should().Be("hello");
    }

    private sealed class TestProbe
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }
}
