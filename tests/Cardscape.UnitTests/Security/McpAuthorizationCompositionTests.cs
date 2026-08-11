using Cardscape.Mcp.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

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

        filters.CallToolFilters.Should().ContainSingle();
        filters.ListResourceTemplatesFilters.Should().ContainSingle();
        filters.ListResourcesFilters.Should().ContainSingle();
        filters.ReadResourceFilters.Should().ContainSingle();
        filters.ListPromptsFilters.Should().ContainSingle();
        filters.GetPromptFilters.Should().ContainSingle();
        filters.CompleteFilters.Should().ContainSingle();
        filters.SubscribeToResourcesFilters.Should().ContainSingle();
        filters.UnsubscribeFromResourcesFilters.Should().ContainSingle();
    }
}
