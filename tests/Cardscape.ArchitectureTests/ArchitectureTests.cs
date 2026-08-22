using System.Xml.Linq;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;
using TestResult = NetArchTest.Rules.TestResult;

namespace Cardscape.ArchitectureTests;

/// <summary>
/// NetArchTest rules. These tests fail the build if the Clean
/// Architecture dependency graph drifts. The rules are the
/// compiled form of <c>docs/architecture/00-overview.md</c> and
/// the "no corners cut" rule from <c>docs/AGENTS.md</c>.
/// </summary>
public sealed class ArchitectureTests
{
    [Fact]
    public void Product_ContainsNoDevelopmentPrivilegeBypass()
    {
        string[] forbiddenTypes =
        [
            "DevOnlyEndpoints",
            "DevDisableTotpCommand",
            "DevDisableTotpCommandHandler",
            "PromoteSelfToAdminCommand",
            "PromoteSelfToAdminCommandHandler"
        ];
        Type[] matches = new[]
            {
                typeof(Cardscape.Application.Cards.CardscapeExtensions).Assembly,
                typeof(Cardscape.Api.Endpoints.Auth.AuthEndpoints).Assembly
            }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => forbiddenTypes.Contains(type.Name, StringComparer.Ordinal))
            .ToArray();

        matches.Should().BeEmpty();
    }

    [Fact]
    public void Product_ContainsNoSimulatedAiProvider()
    {
        Type[] matches = typeof(Cardscape.Infrastructure.DependencyInjection.InfrastructureServiceCollectionExtensions)
            .Assembly.GetTypes()
            .Where(type => type.Name == "RuleBasedAiService")
            .ToArray();

        matches.Should().BeEmpty();
    }

    [Fact]
    public void Application_AiPortContainsOnlyConsumedCompletionCapability()
    {
        typeof(Cardscape.Application.Abstractions.IAiService)
            .GetMethods()
            .Select(method => method.Name)
            .Should().Equal("CompleteAsync");

        string[] forbiddenTypes = ["AiMessage", "AiChatCompletion", "AiEmbedding"];
        Type[] matches = typeof(Cardscape.Application.Cards.CardscapeExtensions)
            .Assembly.GetTypes()
            .Where(type => forbiddenTypes.Contains(type.Name, StringComparer.Ordinal))
            .ToArray();

        matches.Should().BeEmpty();
    }

    [Fact]
    public void Product_ContainsNoVolatileSearchIndex()
    {
        string[] forbiddenTypes = ["ISearchIndex", "InMemorySearchIndex", "FakeSearchIndex"];
        Type[] matches = new[]
            {
                typeof(Cardscape.Application.Cards.CardscapeExtensions).Assembly,
                typeof(Cardscape.Infrastructure.DependencyInjection.InfrastructureServiceCollectionExtensions).Assembly
            }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => forbiddenTypes.Contains(type.Name, StringComparer.Ordinal))
            .ToArray();

        matches.Should().BeEmpty();
    }

    [Fact]
    public void Product_ContainsNoSimulatedGenericEmailTransport()
    {
        string[] forbiddenTypes = ["IEmailService", "EmailMessage", "ConsoleEmailService"];
        Type[] matches = new[]
            {
                typeof(Cardscape.Application.Cards.CardscapeExtensions).Assembly,
                typeof(Cardscape.Infrastructure.DependencyInjection.InfrastructureServiceCollectionExtensions).Assembly
            }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => forbiddenTypes.Contains(type.Name, StringComparer.Ordinal))
            .ToArray();

        matches.Should().BeEmpty();
    }

    [Fact]
    public void Product_ContainsNoSimulatedInvitationEmailTransport()
    {
        string[] forbiddenTypes = ["IInvitationEmailService", "ConsoleInvitationEmailService"];
        Type[] matches = new[]
            {
                typeof(Cardscape.Application.Cards.CardscapeExtensions).Assembly,
                typeof(Cardscape.Infrastructure.DependencyInjection.InfrastructureServiceCollectionExtensions).Assembly
            }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => forbiddenTypes.Contains(type.Name, StringComparer.Ordinal))
            .ToArray();

        matches.Should().BeEmpty();
    }

    [Fact]
    public void Infrastructure_ContainsNoPlaceholderLogSinks()
    {
        string[] forbiddenTypes = ["DatabaseLogSink", "DatabaseLogSinkOptions"];
        Type[] matches = typeof(Cardscape.Infrastructure.DependencyInjection.InfrastructureServiceCollectionExtensions)
            .Assembly.GetTypes()
            .Where(type => forbiddenTypes.Contains(type.Name, StringComparer.Ordinal))
            .ToArray();

        matches.Should().BeEmpty();
    }

    [Fact]
    public void Application_HasSingleCanonicalMirrorCardCommand()
    {
        Type[] commands = typeof(Cardscape.Application.Cards.CardscapeExtensions)
            .Assembly.GetTypes()
            .Where(type => type.Name == "MirrorCardCommand")
            .ToArray();

        commands.Should().ContainSingle()
            .Which.DeclaringType.Should().Be(typeof(Cardscape.Application.Cards.CardscapeExtensions));
    }

    private const string Domain = "Cardscape.Domain";
    private const string Application = "Cardscape.Application";
    private const string Infrastructure = "Cardscape.Infrastructure";
    private const string Api = "Cardscape.Api";
    private const string Web = "Cardscape.Web";
    private const string Mcp = "Cardscape.Mcp";

    [Fact]
    public void SourceProjects_HaveOnlyTheApprovedDirectProjectReferences()
    {
        // Api -> Web is the deliberate exception to the inward-only graph: the
        // ASP.NET Core host serves the Blazor WebAssembly client. Reading the
        // project files catches reference drift even before code uses a type.
        IReadOnlyDictionary<string, string[]> approvedReferences =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Cardscape.Domain"] = [],
                ["Cardscape.Application"] = ["Cardscape.Domain"],
                ["Cardscape.Infrastructure"] = ["Cardscape.Application"],
                ["Cardscape.Migrations.PostgreSql"] = ["Cardscape.Infrastructure"],
                ["Cardscape.Migrations.MySql"] = ["Cardscape.Infrastructure"],
                ["Cardscape.Web"] = [],
                ["Cardscape.Seeder"] = ["Cardscape.Application", "Cardscape.Domain", "Cardscape.Infrastructure"],
                ["Cardscape.Mcp"] = ["Cardscape.Application", "Cardscape.Infrastructure"],
                ["Cardscape.Api"] = ["Cardscape.Application", "Cardscape.Infrastructure", "Cardscape.Migrations.MySql", "Cardscape.Migrations.PostgreSql", "Cardscape.Seeder", "Cardscape.Web"],
            };

        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string[] projectFiles = Directory.GetFiles(
            Path.Combine(repositoryRoot.FullName, "src"),
            "*.csproj",
            SearchOption.AllDirectories);

        var actualReferences = projectFiles.ToDictionary(
            projectFile => Path.GetFileNameWithoutExtension(projectFile),
            ReadDirectProjectReferences,
            StringComparer.Ordinal);

        actualReferences.Keys.Should().BeEquivalentTo(approvedReferences.Keys);
        foreach ((string project, string[] expectedReferences) in approvedReferences)
        {
            actualReferences[project].Should().BeEquivalentTo(
                expectedReferences,
                $"{project} may only reference its approved direct dependencies");
        }
    }

    private static string[] ReadDirectProjectReferences(string projectFile)
    {
        XDocument project = XDocument.Load(projectFile);
        return project.Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFileNameWithoutExtension(path!))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new DirectoryNotFoundException(
            $"Could not find the repository root from {AppContext.BaseDirectory}.");
    }

    [Fact]
    public void Domain_DoesNotDependOn_AnyOuterLayer()
    {
        TestResult result = Types.InAssembly(typeof(Cardscape.Domain.Common.AggregateRoot<>).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                Application,
                Infrastructure,
                Api,
                Web,
                Mcp)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Domain must be a pure C# library. Offenders: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Application_DependsOn_Domain_Only()
    {
        TestResult result = Types.InAssembly(typeof(Cardscape.Application.Abstractions.IClock).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                Infrastructure,
                Api,
                Web,
                Mcp)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Application must not reference any outer layer. Offenders: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Infrastructure_DependsOn_ApplicationAndDomain_Only()
    {
        TestResult result = Types.InAssembly(typeof(Cardscape.Infrastructure.DependencyInjection.InfrastructureServiceCollectionExtensions).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                Api,
                Web,
                Mcp)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Infrastructure must not reference the presentation or MCP layers. Offenders: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Api_WebProjectReference_IsUsedForHostingOnly()
    {
        // The API project is a Microsoft.NET.Sdk.Web top-level program; the
        // generated `Program` type is internal to the assembly. We anchor
        // the assembly lookup on a real public type (ServiceCollectionExtensions)
        // and assert that application code does not couple to Web types. The
        // ProjectReference itself is intentional and separately guarded above.
        var apiAssembly = typeof(Cardscape.Api.Extensions.ServiceCollectionExtensions).Assembly;

        TestResult result = Types.InAssembly(apiAssembly)
            .ShouldNot()
            .HaveDependencyOn(Web)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Api may host Web assets but must not depend on Web types. Offenders: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Web_DependsOnNothing_BeyondItself()
    {
        // Web is a Blazor WASM client. It must NOT take a project
        // reference to the API, the Infrastructure, the Application, or
        // the MCP projects. The client talks to the API over HTTP at
        // runtime; DTOs are intentionally duplicated in Cardscape.Web.Shared
        // (per docs/development/02-vertical-slices.md §2 step 7).
        var webAssembly = typeof(Cardscape.Web.App).Assembly;

        // NetArchTest's HaveDependencyOn does a substring match on the
        // referenced type's full name. To disambiguate from the
        // "Cardscape.Web.Services.Api" sub-namespace we use a trailing
        // period on each module name.
        TestResult result = Types.InAssembly(webAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Cardscape.Application.",
                "Cardscape.Infrastructure.",
                "Cardscape.Api.",
                "Cardscape.Mcp.")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Web is a Blazor WASM client. It must not reference the API/Infrastructure/Application/MCP projects. Offenders: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Mcp_DependsOn_ApplicationInfrastructureDomain_Only()
    {
        // The MCP server is a separate process that talks to the same
        // Application layer the API uses. It must NOT take a project
        // reference to the API or the Web client.
        var mcpAssembly = typeof(Cardscape.Mcp.Extensions.ServiceCollectionExtensions).Assembly;

        TestResult result = Types.InAssembly(mcpAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                Api,
                Web)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"MCP server must not reference the API or the Web client. Offenders: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Mcp_DoesNotReimplementCurrentUser()
    {
        var mcpAssembly = typeof(Cardscape.Mcp.Extensions.ServiceCollectionExtensions).Assembly;
        Type currentUserContract = typeof(Cardscape.Application.Abstractions.Security.ICurrentUser);

        string[] implementations = mcpAssembly.GetTypes()
            .Where(type => type.IsClass && currentUserContract.IsAssignableFrom(type))
            .Select(type => type.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        implementations.Should().BeEmpty(
            "hosts should adapt ClaimsPrincipal through ICurrentUserAccessor and reuse Application's CurrentUser mapping");
    }

    [Fact]
    public void Mcp_EveryToolHasExactlyOneExplicitScopeClassification()
    {
        var mcpAssembly = typeof(Cardscape.Mcp.Extensions.ServiceCollectionExtensions).Assembly;

        string[] advertisedTools = mcpAssembly.GetTypes()
            .SelectMany(type => type.GetMethods())
            .Select(method => method.GetCustomAttributes(typeof(ModelContextProtocol.Server.McpServerToolAttribute), false)
                .Cast<ModelContextProtocol.Server.McpServerToolAttribute>()
                .SingleOrDefault()?.Name)
            .Where(name => name is not null)
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] classifiedTools = Cardscape.Mcp.Authorization.McpToolScopePolicy.RequiredScopes.Keys
            .Order(StringComparer.Ordinal)
            .ToArray();

        classifiedTools.Should().Equal(
            advertisedTools,
            "the closed MCP scope catalog must classify every advertised tool and contain no stale entries");
    }

    [Fact]
    public void Mcp_DoesNotAdvertiseRemovedCompatibilityAliases()
    {
        string[] advertisedTools = typeof(Cardscape.Mcp.Extensions.ServiceCollectionExtensions).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods())
            .SelectMany(method => method
                .GetCustomAttributes(typeof(ModelContextProtocol.Server.McpServerToolAttribute), false)
                .Cast<ModelContextProtocol.Server.McpServerToolAttribute>())
            .Select(attribute => attribute.Name)
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();

        advertisedTools.Should().NotContain("members_assign");
        advertisedTools.Should().ContainSingle(name => name == "cards_assign");
    }

    [Fact]
    public void GoogleCalendarPage_ExposesOnlyCanonicalIntegrationRoute()
    {
        string[] routes = typeof(Cardscape.Web.Pages.GoogleCalendar)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.Components.RouteAttribute>()
            .Select(attribute => attribute.Template)
            .ToArray();

        routes.Should().Equal("/settings/integrations/google-calendar");
    }

    [Fact]
    public void WebActivityKind_MatchesDomainWireEnumExactly()
    {
        Enum.GetNames<Cardscape.Web.Shared.ActivityKind>()
            .Should().Equal(Enum.GetNames<Cardscape.Domain.Activities.ActivityKind>());
        Enum.GetValues<Cardscape.Web.Shared.ActivityKind>().Select(value => (int)value)
            .Should().Equal(Enum.GetValues<Cardscape.Domain.Activities.ActivityKind>().Select(value => (int)value));
    }

    [Fact]
    public void Domain_Entities_AreSealed()
    {
        // Aggregates and entities are sealed unless polymorphism is required.
        // The only allowed exceptions are abstract base classes in Common/
        // (AggregateRoot, Entity, ValueObject) which are by design unsealed.
        TestResult result = Types.InAssembly(typeof(Cardscape.Domain.Common.AggregateRoot<>).Assembly)
            .That()
            .ResideInNamespace("Cardscape.Domain")
            .And()
            .DoNotResideInNamespace("Cardscape.Domain.Common")
            .And()
            .AreClasses()
            .And()
            .DoNotHaveNameEndingWith("Errors")
            .And()
            .DoNotHaveNameEndingWith("Events")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Domain entities/aggregates must be sealed. Offenders: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Application_Handlers_AreSealed()
    {
        // Only classes (not interfaces) ending in "Handler" must be sealed.
        // The `IBackgroundJobHandler` etc. are interfaces by design and
        // can't be sealed — they fall outside the rule.
        TestResult result = Types.InAssembly(typeof(Cardscape.Application.Abstractions.IClock).Assembly)
            .That()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Handler")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Application handlers (classes) must be sealed. Offenders: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Application_Abstractions_UseInterfaceNamingConvention()
    {
        TestResult result = Types.InAssembly(typeof(Cardscape.Application.Abstractions.IClock).Assembly)
            .That()
            .ResideInNamespace("Cardscape.Application.Abstractions")
            .And()
            .AreInterfaces()
            .Should()
            .HaveNameStartingWith("I")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Application abstractions must use the interface naming convention. Offenders: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Application_PublicInterfaces_ResideUnderAbstractionsNamespace()
    {
        // Application owns its public ports. Keeping every port below one
        // namespace makes that boundary discoverable and prevents feature or
        // implementation namespaces from becoming accidental contract roots.
        string[] misplacedInterfaces = typeof(Cardscape.Application.Abstractions.IClock).Assembly
            .GetTypes()
            .Where(type => type.IsInterface && (type.IsPublic || type.IsNestedPublic))
            .Where(type => type.Namespace is null ||
                !(type.Namespace.Equals("Cardscape.Application.Abstractions", StringComparison.Ordinal) ||
                  type.Namespace.StartsWith("Cardscape.Application.Abstractions.", StringComparison.Ordinal)))
            .Select(type => type.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        misplacedInterfaces.Should().BeEmpty(
            "all public Application ports must live under Cardscape.Application.Abstractions; " +
            "legacy aliases in feature namespaces are not supported");
    }

    [Fact]
    public void Infrastructure_DeclaresNoPublicInterfaces()
    {
        // Infrastructure implements ports owned by Application. Public interfaces
        // declared here invert that ownership and create abstractions around
        // implementation details. If a genuine port is needed, it belongs in the
        // consuming inner layer instead.
        var allInfrastructureTypes = Types.InAssembly(typeof(Cardscape.Infrastructure.DependencyInjection.InfrastructureServiceCollectionExtensions).Assembly)
            .That()
            .AreInterfaces()
            .GetTypes();

        var orphanNames = allInfrastructureTypes
            .Where(i => i.IsPublic)
            .Select(i => i.FullName)
            .ToList();

        orphanNames.Should().BeEmpty(
            $"Infrastructure must not introduce new public interfaces — they belong in Application/Abstractions. Offenders: {string.Join(", ", orphanNames)}");
    }

    [Fact]
    public void Seeder_DeclaresNoPublicInterfaces()
    {
        // Seeder is an optional implementation module composed by the API.
        // Its pipeline steps are internal details, while the small concrete
        // surface consumed by the API (runner, options and report) stays public.
        var publicInterfaces = Types.InAssembly(typeof(Cardscape.Seeder.SeedRunner).Assembly)
            .That()
            .AreInterfaces()
            .GetTypes()
            .Where(type => type.IsPublic)
            .Select(type => type.FullName)
            .ToList();

        publicInterfaces.Should().BeEmpty(
            $"Seeder must not expose implementation interfaces. Offenders: {string.Join(", ", publicInterfaces)}");
    }
}
