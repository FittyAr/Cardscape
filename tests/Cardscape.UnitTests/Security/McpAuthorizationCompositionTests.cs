using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Members;
using Cardscape.Mcp.Authorization;
using Cardscape.Mcp.Extensions;
using Cardscape.Mcp.Idempotency;
using Cardscape.Tests.Common.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace Cardscape.UnitTests.Security;

public sealed class McpAuthorizationCompositionTests
{
    [Fact]
    public void AddCardscapeMcp_RegistersScopeFiltersForEveryDataBearingSurface()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Data Source=:memory:"
            })
            .Build();
        services.AddLogging();

        services.AddCardscapeMcp(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        McpRequestFilters filters = provider.GetRequiredService<IOptions<McpServerOptions>>()
            .Value.Filters.Request;

        filters.CallToolFilters.Should().ContainSingle(
            "authorization and idempotency must share one ordered tools/call boundary");
        filters.ListResourceTemplatesFilters.Should().ContainSingle();
        filters.ListResourcesFilters.Should().ContainSingle();
        filters.ReadResourceFilters.Should().ContainSingle();
        filters.ListPromptsFilters.Should().ContainSingle();
        filters.GetPromptFilters.Should().ContainSingle();
        filters.CompleteFilters.Should().ContainSingle();
        filters.SubscribeToResourcesFilters.Should().ContainSingle();
        filters.UnsubscribeFromResourcesFilters.Should().ContainSingle();
    }

    [Fact]
    public async Task AddCardscapeMcp_CallToolFilter_AuthorizesThenReplaysCataloguedWrite()
    {
        McpRequestFilters filters = BuildFilters();
        var store = new InMemoryIdempotencyKeyStore();
        var currentUser = new FakeCurrentUser
        {
            IsAuthenticated = true,
            Id = new UserId(Guid.NewGuid())
        };
        var accessor = new Mock<ICurrentUserAccessor>();
        accessor.Setup(instance => instance.GetCurrentPrincipal())
            .Returns(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(McpToolScopePolicy.ScopeClaimType, "write")],
                "ApiToken")));
        var requestServices = new ServiceCollection()
            .AddSingleton(accessor.Object)
            .AddSingleton<ICurrentUser>(currentUser)
            .AddSingleton<IIdempotencyKeyStore>(store)
            .AddSingleton<IClock>(new FakeClock())
            .BuildServiceProvider();
        var server = new Mock<McpServer>();
        server.SetupGet(instance => instance.Services).Returns(requestServices);
        var parameters = new CallToolRequestParams
        {
            Name = "boards_create",
            Arguments = new Dictionary<string, JsonElement>
            {
                ["name"] = JsonDocument.Parse("\"Roadmap\"").RootElement.Clone()
            },
            Meta = new JsonObject
            {
                [McpToolIdempotencyPolicy.MetaPropertyName] = "composition-request-1234"
            }
        };
        var calls = 0;
        McpRequestHandler<CallToolRequestParams, CallToolResult> pipeline =
            filters.CallToolFilters.Single()(async (_, _) =>
            {
                calls++;
                await Task.Yield();
                return new CallToolResult
                {
                    Content = [new TextContentBlock { Text = $"call-{calls}" }]
                };
            });

        CallToolResult first = await pipeline(
            new RequestContext<CallToolRequestParams>(
                server.Object, new JsonRpcRequest { Method = "tools/call" }, parameters),
            CancellationToken.None);
        CallToolResult replay = await pipeline(
            new RequestContext<CallToolRequestParams>(
                server.Object, new JsonRpcRequest { Method = "tools/call" }, parameters),
            CancellationToken.None);

        calls.Should().Be(1);
        store.Count.Should().Be(1);
        replay.Should().BeEquivalentTo(first);
    }

    private static McpRequestFilters BuildFilters()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Data Source=:memory:"
            })
            .Build();
        services.AddLogging();
        services.AddCardscapeMcp(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<McpServerOptions>>()
            .Value.Filters.Request;
    }
}
