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
    private const string Domain = "Cardscape.Domain";
    private const string Application = "Cardscape.Application";
    private const string Infrastructure = "Cardscape.Infrastructure";
    private const string Api = "Cardscape.Api";
    private const string Web = "Cardscape.Web";
    private const string Mcp = "Cardscape.Mcp";

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
    public void Api_DependsOn_ApplicationInfrastructureDomain_Only()
    {
        // The API project is a Microsoft.NET.Sdk.Web top-level program; the
        // generated `Program` type is internal to the assembly. We anchor
        // the assembly lookup on a real public type (ServiceCollectionExtensions)
        // and assert that none of its public surface depends on Web.
        var apiAssembly = typeof(Cardscape.Api.Extensions.ServiceCollectionExtensions).Assembly;

        TestResult result = Types.InAssembly(apiAssembly)
            .ShouldNot()
            .HaveDependencyOn(Web)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Api is the server host. The Web client talks to it over HTTP, never via a project reference. Offenders: {string.Join(", ", result.FailingTypeNames ?? [])}");
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
    public void Application_Abstractions_Live_Under_Abstractions_Namespace()
    {
        // Every public interface that lives in Application/Abstractions/ must
        // start with 'I'. Catches accidental public surface without the I prefix.
        TestResult result = Types.InAssembly(typeof(Cardscape.Application.Abstractions.IClock).Assembly)
            .That()
            .ResideInNamespace("Cardscape.Application.Abstractions")
            .And()
            .AreInterfaces()
            .Should()
            .HaveNameStartingWith("I")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"All interfaces in Application/Abstractions must start with I. Offenders: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Infrastructure_HasNoOrphanInterfaces()
    {
        // Every interface defined in Infrastructure must have a non-Infrastructure
        // counterpart (i.e. it implements an Application/Abstractions/ interface).
        // A green test means there are no Infrastructure-only interfaces that the
        // rest of the codebase couldn't mock.
        var allInfrastructureTypes = Types.InAssembly(typeof(Cardscape.Infrastructure.DependencyInjection.InfrastructureServiceCollectionExtensions).Assembly)
            .That()
            .AreInterfaces()
            .GetTypes();

        var orphanNames = allInfrastructureTypes
            .Where(i => i.IsPublic)
            .Where(i => !i.Name.StartsWith('I'))
            .Select(i => i.FullName)
            .ToList();

        orphanNames.Should().BeEmpty(
            $"Infrastructure must not introduce new public interfaces — they belong in Application/Abstractions. Offenders: {string.Join(", ", orphanNames)}");
    }
}
