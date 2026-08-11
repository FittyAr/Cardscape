using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Members;
using Cardscape.Mcp.Authentication;
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
        filters.ListResourceTemplatesFilters.Where(IsReadScopeFilter).Should().ContainSingle();
        filters.ListResourcesFilters.Where(IsReadScopeFilter).Should().ContainSingle();
        filters.ReadResourceFilters.Where(IsReadScopeFilter).Should().ContainSingle();
        filters.ListPromptsFilters.Where(IsReadScopeFilter).Should().ContainSingle();
        filters.GetPromptFilters.Where(IsReadScopeFilter).Should().ContainSingle();
        filters.CompleteFilters.Where(IsReadScopeFilter).Should().ContainSingle();
        filters.SubscribeToResourcesFilters.Where(IsReadScopeFilter).Should().ContainSingle();
        filters.UnsubscribeFromResourcesFilters.Where(IsReadScopeFilter).Should().ContainSingle();
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
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(McpToolScopePolicy.ScopeClaimType, "write")],
            "ApiToken"));
        var accessor = new McpRequestCurrentUserAccessor();
        var requestServices = new ServiceCollection()
            .AddSingleton(accessor)
            .AddSingleton<ICurrentUserAccessor>(accessor)
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
                accessor.GetCurrentPrincipal().Should().BeSameAs(principal);
                calls++;
                await Task.Yield();
                return new CallToolResult
                {
                    Content = [new TextContentBlock { Text = $"call-{calls}" }]
                };
            });

        var request = new RequestContext<CallToolRequestParams>(
            server.Object, new JsonRpcRequest { Method = "tools/call" }, parameters)
        {
            User = principal
        };

        CallToolResult first = await pipeline(request, CancellationToken.None);
        CallToolResult replay = await pipeline(
            new RequestContext<CallToolRequestParams>(
                server.Object, new JsonRpcRequest { Method = "tools/call" }, parameters)
            {
                User = principal
            },
            CancellationToken.None);

        calls.Should().Be(1);
        store.Count.Should().Be(1);
        replay.Should().BeEquivalentTo(first);
    }

    [Fact]
    public void AddCardscapeMcp_RequestIdentityFlowsIntoNestedToolScope()
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
        Guid userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            "ApiToken"));

        using (IServiceScope requestScope = provider.CreateScope())
        {
            requestScope.ServiceProvider
                .GetRequiredService<McpRequestCurrentUserAccessor>()
                .SetCurrentPrincipal(principal);
        }

        using IServiceScope toolScope = provider.CreateScope();
        ICurrentUser currentUser = toolScope.ServiceProvider.GetRequiredService<ICurrentUser>();

        currentUser.IsAuthenticated.Should().BeTrue();
        currentUser.Id.Should().Be(new UserId(userId));
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

    private static bool IsReadScopeFilter(Delegate filter) =>
        filter.Method.Name == "RequireReadScope";
}
