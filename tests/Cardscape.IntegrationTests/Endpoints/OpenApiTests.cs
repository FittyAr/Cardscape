using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cardscape.IntegrationTests.Fixtures;
using Sdk = Cardscape.Sdk;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Regression coverage for the OpenAPI document generation.
/// The document is produced by the native .NET 10+
/// <c>Microsoft.AspNetCore.OpenApi</c> generator (added via
/// <c>AddOpenApi()</c> in <c>Program.cs</c>) and served at
/// <c>GET /openapi/v1.json</c> in the Development environment.
/// The Scalar reference UI (<c>/scalar</c>) renders on top of
/// that same document. This test pins the contract so a future
/// refactor cannot silently break the public API surface that
/// SDK generators, the MCP server, and third-party consumers
/// read from.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class OpenApiTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public OpenApiTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task OpenApi_Document_Builds_And_Is_Served_As_Valid_Json()
    {
        HttpClient client = _factory.CreateApiClient();

        HttpResponseMessage response = await client.GetAsync("openapi/v1.json", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotBeNullOrWhiteSpace();

        // Must be parseable JSON. If the generator blew up while
        // building the document the middleware would have
        // replaced the body with a Problem Details payload and
        // the parse would fail.
        using JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("openapi", out JsonElement openapi).Should().BeTrue();
        openapi.GetString().Should().StartWith("3.");
        doc.RootElement.TryGetProperty("paths", out JsonElement paths).Should().BeTrue();
        paths.EnumerateObject().Should().NotBeEmpty("every endpoint should be documented");
    }

    [Fact]
    public async Task OpenApi_Document_Exposes_Bearer_Security_Scheme()
    {
        HttpClient client = _factory.CreateApiClient();

        HttpResponseMessage response = await client.GetAsync("openapi/v1.json", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("components", out JsonElement components).Should().BeTrue();
        components.TryGetProperty("securitySchemes", out JsonElement schemes).Should().BeTrue();
        schemes.TryGetProperty("Bearer", out JsonElement bearer).Should().BeTrue(
            "the Bearer scheme is contributed by BearerSecuritySchemeTransformer so Scalar renders the Authorize button");
        bearer.GetProperty("type").GetString().Should().Be("http");
        bearer.GetProperty("scheme").GetString().Should().Be("bearer");
        bearer.GetProperty("bearerFormat").GetString().Should().Be("JWT");
    }

    [Fact]
    public async Task Scalar_Reference_Is_Served_From_The_Canonical_OpenApi_Document()
    {
        HttpClient client = _factory.CreateApiClient();

        using HttpResponseMessage response = await client.GetAsync(
            "scalar", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("\"url\":\"openapi/v1.json\"");
    }

    [Fact]
    public async Task OpenApi_ClientSuccessSchemas_Match_Sdk_And_Web_Models()
    {
        (string Path, string Method, string Status, Type Model, bool Collection)[] contracts =
        [
            ("/api/workspaces", "get", "200", typeof(Sdk.WorkspaceDto), true),
            ("/api/workspaces/{workspaceId}", "get", "200", typeof(Sdk.WorkspaceDto), false),
            ("/api/workspaces", "post", "201", typeof(Sdk.WorkspaceDto), false),
            ("/api/workspaces/{workspaceId}/members", "get", "200", typeof(Sdk.WorkspaceMemberDto), true),
            ("/api/boards", "get", "200", typeof(Sdk.BoardSummaryDto), true),
            ("/api/boards/{boardId}", "get", "200", typeof(Sdk.BoardDto), false),
            ("/api/boards", "post", "201", typeof(Sdk.BoardDto), false),
            ("/api/lists", "get", "200", typeof(Sdk.BoardListDto), true),
            ("/api/lists/{listId}", "get", "200", typeof(Sdk.BoardListDto), false),
            ("/api/lists", "post", "201", typeof(Sdk.BoardListDto), false),
            ("/api/cards", "get", "200", typeof(Sdk.CardSummaryDto), true),
            ("/api/cards/{cardId}", "get", "200", typeof(Sdk.CardDto), false),
            ("/api/cards", "post", "201", typeof(Sdk.CardDto), false),
            ("/api/boards/{boardId}/labels", "get", "200", typeof(Sdk.LabelDto), true),
            ("/api/boards/{boardId}/labels", "post", "201", typeof(Sdk.LabelDto), false),
            ("/api/cards/{cardId}/comments", "get", "200", typeof(Sdk.CommentDto), true),
            ("/api/cards/{cardId}/comments", "post", "201", typeof(Sdk.CommentDto), false),
            ("/api/boards/{boardId}/activities", "get", "200", typeof(Sdk.ActivityPageDto), false),
        ];
        Dictionary<Type, Type> webModels = new()
        {
            [typeof(Sdk.WorkspaceDto)] = typeof(Cardscape.Web.Shared.WorkspaceDto),
            [typeof(Sdk.WorkspaceMemberDto)] = typeof(Cardscape.Web.Shared.WorkspaceMemberDto),
            [typeof(Sdk.BoardSummaryDto)] = typeof(Cardscape.Web.Shared.BoardSummaryDto),
            [typeof(Sdk.BoardDto)] = typeof(Cardscape.Web.Shared.BoardDto),
            [typeof(Sdk.BoardListDto)] = typeof(Cardscape.Web.Shared.BoardListDto),
            [typeof(Sdk.CardSummaryDto)] = typeof(Cardscape.Web.Shared.CardSummaryDto),
            [typeof(Sdk.CardDto)] = typeof(Cardscape.Web.Shared.CardDto),
            [typeof(Sdk.LabelDto)] = typeof(Cardscape.Web.Shared.LabelDto),
            [typeof(Sdk.CommentDto)] = typeof(Cardscape.Web.Shared.CommentDto),
            [typeof(Sdk.ActivityPageDto)] = typeof(Cardscape.Web.Shared.ActivityPageDto),
        };

        HttpClient client = _factory.CreateApiClient();
        using HttpResponseMessage response = await client.GetAsync(
            "openapi/v1.json", TestContext.Current.CancellationToken);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));

        foreach ((string path, string method, string status, Type sdkModel, bool collection) in contracts)
        {
            JsonElement schema = doc.RootElement
                .GetProperty("paths")
                .GetProperty(path)
                .GetProperty(method)
                .GetProperty("responses")
                .GetProperty(status)
                .GetProperty("content")
                .GetProperty("application/json")
                .GetProperty("schema");
            schema = ResolveSchema(doc, schema);
            if (collection)
            {
                schema.GetProperty("type").GetString().Should().Be(
                    "array", $"{method.ToUpperInvariant()} {path} {status} returns a JSON collection");
                schema = ResolveSchema(doc, schema.GetProperty("items"));
            }

            string[] openApiProperties = schema
                .GetProperty("properties")
                .EnumerateObject()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal)
                .ToArray();
            string[] sdkProperties = sdkModel.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                    ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name))
                .Order(StringComparer.Ordinal)
                .ToArray();

            openApiProperties.Should().Equal(sdkProperties,
                $"{method.ToUpperInvariant()} {path} {status} is the canonical SDK wire contract");
            string[] webProperties = webModels[sdkModel]
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                    ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name))
                .Order(StringComparer.Ordinal)
                .ToArray();
            openApiProperties.Should().Equal(webProperties,
                $"{method.ToUpperInvariant()} {path} {status} is the canonical Web wire contract");
        }
    }

    [Fact]
    public async Task OpenApi_AuthSuccessResponses_Match_Their_WireContracts()
    {
        (string Path, string Method, string Status, Type? Model)[] contracts =
        [
            ("/api/auth/register", "post", "201", typeof(Cardscape.Application.Authentication.DTOs.AuthResponse)),
            ("/api/auth/login", "post", "200", typeof(Cardscape.Application.Authentication.DTOs.AuthResponse)),
            ("/api/auth/forgot-password", "post", "200", typeof(Cardscape.Application.Authentication.Commands.PasswordResetRequestResult)),
            ("/api/auth/reset-password", "post", "204", null),
            ("/api/auth/login/totp", "post", "200", typeof(Cardscape.Application.Authentication.DTOs.AuthResponse)),
            ("/api/auth/me", "get", "200", typeof(Cardscape.Application.Authentication.DTOs.UserSummary)),
            ("/api/auth/revoke", "post", "204", null),
            ("/api/auth/2fa/status", "get", "200", typeof(Cardscape.Application.Abstractions.Authentication.TotpStatus)),
            ("/api/auth/2fa/enroll", "post", "200", typeof(Cardscape.Api.Endpoints.Auth.TotpEnrollmentResponse)),
            ("/api/auth/2fa/verify", "post", "200", typeof(Cardscape.Api.Endpoints.Auth.TotpVerificationResponse)),
            ("/api/auth/2fa/confirm", "post", "204", null),
            ("/api/auth/2fa/disable", "post", "204", null),
        ];

        HttpClient client = _factory.CreateApiClient();
        using HttpResponseMessage response = await client.GetAsync(
            "openapi/v1.json", TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));

        foreach ((string path, string method, string status, Type? model) in contracts)
        {
            JsonElement success = document.RootElement
                .GetProperty("paths")
                .GetProperty(path)
                .GetProperty(method)
                .GetProperty("responses")
                .GetProperty(status);
            if (model is null)
            {
                success.TryGetProperty("content", out _).Should().BeFalse(
                    $"{method.ToUpperInvariant()} {path} {status} has no response body");
                continue;
            }

            JsonElement schema = ResolveSchema(document, success
                .GetProperty("content")
                .GetProperty("application/json")
                .GetProperty("schema"));
            string[] openApiProperties = schema.GetProperty("properties")
                .EnumerateObject()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal)
                .ToArray();
            string[] modelProperties = model.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                    ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name))
                .Order(StringComparer.Ordinal)
                .ToArray();

            openApiProperties.Should().Equal(modelProperties,
                $"{method.ToUpperInvariant()} {path} {status} is the canonical auth wire contract");
        }
    }

    private static JsonElement ResolveSchema(JsonDocument document, JsonElement schema)
    {
        while (schema.TryGetProperty("$ref", out JsonElement reference))
        {
            string componentName = reference.GetString()!.Split('/')[^1];
            schema = document.RootElement
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty(componentName);
        }

        return schema;
    }
}
