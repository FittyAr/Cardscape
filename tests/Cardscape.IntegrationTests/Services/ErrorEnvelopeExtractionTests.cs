using System.Net;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.IntegrationTests.Fixtures;
using Cardscape.Web.Services;
using FluentAssertions;
using Xunit;

namespace Cardscape.IntegrationTests.Services;

/// <summary>
/// The API has historically shipped three different error
/// envelope shapes depending on the endpoint group (RFC 7807
/// ProblemDetails for Auth, flat <c>{code, message}</c> for
/// Activity/Ai/Slack/Automation, wrapped
/// <c>{error: {code, message}}</c> for Workspaces). The Web
/// side has to render every one of them as a single
/// user-facing string — the previous implementation only
/// understood the RFC 7807 shape and fell through to the raw
/// body for the other two, which surfaced as
/// <c>{"code":"...","message":"..."}</c> pasted verbatim into
/// the red error alert. The tests below pin each shape to
/// the expected user-facing string so the regression cannot
/// return silently.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class ErrorEnvelopeExtractionTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public ErrorEnvelopeExtractionTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task WrappedEnvelope_Shape3_ReturnsJustTheMessage()
    {
        // POST to workspaces with an empty name → Workspaces
        // MapError wraps the domain error in
        //   { "error": { "code": "...", "message": "..." } }
        // and returns 422. The Web must surface only the
        // message, not the JSON envelope.
        HttpClient client = await CreateAuthenticatedClientAsync();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/workspaces/", new { name = string.Empty }, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        string? result = await AuthService.ExtractErrorAsync(response, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Should().NotContain("{");
        result.Should().NotContain("\"code\"");
        result.Should().NotContain("workspaces.name_required");
        // The message itself is the only thing the user needs.
        // Any of the four asserts above is enough to catch the
        // regression; the "required" check is a sanity guard so
        // a future parser change cannot strip the message and
        // pass.
        result.Should().Contain("required");
    }

    [Fact]
    public async Task FlatShape_Shape2_ReturnsJustTheMessage()
    {
        // Activity, Ai, Slack, Automation etc. all use the
        // flat shape `{ "code": "...", "message": "..." }`
        // (anonymous projection from
        // `new { error.Code, error.Message }`). The exact
        // endpoint is not important here — we synthesise a
        // response body in the flat shape and feed it to
        // ExtractErrorAsync to verify the parser handles it.
        HttpResponseMessage response = new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                "{\"code\":\"activity.not_found\",\"message\":\"Activity not found.\"}",
                System.Text.Encoding.UTF8,
                "application/json")
        };

        string? result = await AuthService.ExtractErrorAsync(response, TestContext.Current.CancellationToken);

        result.Should().Be("Activity not found.");
    }

    [Fact]
    public async Task Rfc7807Shape_Shape1_ReturnsJustTheMessage()
    {
        // Auth/External/TOTP/Integrations use Results.Problem
        // which serialises to RFC 7807 ProblemDetails.
        // ExtractErrorAsync was originally written for this
        // shape; the regression we are guarding against was
        // the OTHER two shapes falling through to the raw
        // body. This test pins the RFC 7807 path so future
        // refactors cannot break it either.
        HttpResponseMessage response = new(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(
                "{\"title\":\"auth.invalid_credentials\",\"detail\":\"Wrong email or password.\",\"status\":401}",
                System.Text.Encoding.UTF8,
                "application/json")
        };

        string? result = await AuthService.ExtractErrorAsync(response, TestContext.Current.CancellationToken);

        result.Should().Be("Wrong email or password.");
    }

    [Fact]
    public async Task EmptyResponse_ReturnsNull()
    {
        HttpResponseMessage response = new(HttpStatusCode.BadGateway)
        {
            Content = new StringContent(string.Empty)
        };

        string? result = await AuthService.ExtractErrorAsync(response, TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task NonJsonResponse_ReturnsRawBody()
    {
        // The fallback path: a proxy upstream returns plain
        // text. We should still surface SOMETHING (so the
        // alert is not blank), but the raw body is the best
        // we can do.
        HttpResponseMessage response = new(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("upstream timeout", System.Text.Encoding.UTF8, "text/plain")
        };

        string? result = await AuthService.ExtractErrorAsync(response, TestContext.Current.CancellationToken);

        result.Should().Be("upstream timeout");
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"envelope-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Envelope User", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
