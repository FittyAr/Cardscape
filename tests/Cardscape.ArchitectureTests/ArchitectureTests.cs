using System.Text.RegularExpressions;
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
    public void AsyncApiEndpointLambdas_AcceptCancellationToken()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string endpointsRoot = Path.Combine(
            repositoryRoot.FullName, "src", "Cardscape.Api", "Endpoints");
        Regex asyncEndpointSignature = new(
            @"\.Map(?:Get|Post|Put|Patch|Delete)\s*\((?<signature>[\s\S]*?\basync\b[\s\S]*?=>)",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        string[] violations = Directory.GetFiles(endpointsRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => asyncEndpointSignature.Matches(File.ReadAllText(file))
                .Where(match => !match.Groups["signature"].Value.Contains(
                    nameof(CancellationToken), StringComparison.Ordinal))
                .Select(match => Path.GetRelativePath(repositoryRoot.FullName, file)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "every async HTTP endpoint must observe request cancellation explicitly");
    }

    [Fact]
    public void AsyncApplicationHandlers_AcceptCancellationToken()
    {
        string[] violations = typeof(Cardscape.Application.Cards.CardscapeExtensions).Assembly
            .GetTypes()
            .Where(type => type.Name.EndsWith("Handler", StringComparison.Ordinal))
            .SelectMany(type => type.GetMethods(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.DeclaredOnly))
            .Where(method => typeof(Task).IsAssignableFrom(method.ReturnType)
                || method.ReturnType == typeof(ValueTask)
                || method.ReturnType.IsGenericType
                    && method.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
            .Where(method => !method.GetParameters()
                .Any(parameter => parameter.ParameterType == typeof(CancellationToken)))
            .Select(method => $"{method.DeclaringType!.FullName}.{method.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "every asynchronous application handler must propagate Wolverine/request cancellation explicitly");
    }

    [Fact]
    public void WebRazorViews_UseRadzenForInteractiveControls()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string webRoot = Path.Combine(repositoryRoot.FullName, "src", "Cardscape.Web");
        Regex razorComment = new(@"@\*[\s\S]*?\*@", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Regex nativeControl = new(
            @"<(?:button|input|select|textarea|form|table|dialog)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        string[] violations = Directory.GetFiles(webRoot, "*.razor", SearchOption.AllDirectories)
            .Where(file => nativeControl.IsMatch(razorComment.Replace(File.ReadAllText(file), string.Empty)))
            .Select(file => Path.GetRelativePath(repositoryRoot.FullName, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "interactive Web controls must use Radzen components so behavior, theming and accessibility remain consistent");
    }

    [Fact]
    public void WebRazorViews_DoNotEmbedLiteralColors()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string webRoot = Path.Combine(repositoryRoot.FullName, "src", "Cardscape.Web");
        Regex literalColor = new(
            "(?:style|Style)\\s*=\\s*\"[^\"]*#[0-9a-f]{3,8}",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        string[] violations = Directory.GetFiles(webRoot, "*.razor", SearchOption.AllDirectories)
            .Where(file => literalColor.IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(repositoryRoot.FullName, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "Razor views must consume Radzen theme tokens instead of bypassing light/dark themes with literal colors");
    }

    [Fact]
    public void WebRazorViews_DoNotHardcodeAccessibleNames()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string webRoot = Path.Combine(repositoryRoot.FullName, "src", "Cardscape.Web");
        Regex literalAccessibleName = new(
            "aria-label\\s*=\\s*\"(?!@)[^\"]+\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        string[] violations = Directory.GetFiles(webRoot, "*.razor", SearchOption.AllDirectories)
            .Where(file => literalAccessibleName.IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(repositoryRoot.FullName, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "accessible names are user-facing text and must participate in localization");
    }

    [Fact]
    public void WebIconOnlyRadzenButtons_HaveAccessibleNames()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string webRoot = Path.Combine(repositoryRoot.FullName, "src", "Cardscape.Web");
        Regex button = new(
            "<RadzenButton\\b(?<attributes>(?:\"[^\"]*\"|[^>])*)>",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        string[] violations = Directory.GetFiles(webRoot, "*.razor", SearchOption.AllDirectories)
            .SelectMany(file => button.Matches(File.ReadAllText(file))
                .Where(match => !match.Groups["attributes"].Value.Contains("Text=", StringComparison.Ordinal)
                    && !match.Groups["attributes"].Value.Contains("aria-label=", StringComparison.Ordinal))
                .Select(_ => Path.GetRelativePath(repositoryRoot.FullName, file)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "every Radzen button without visible text must expose a localized accessible name");
    }

    [Fact]
    public void WebRoutablePages_UseSharedLocalization()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string pagesRoot = Path.Combine(repositoryRoot.FullName, "src", "Cardscape.Web", "Pages");

        string[] violations = Directory.GetFiles(pagesRoot, "*.razor", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("@page ", StringComparison.Ordinal))
            .Where(file => !File.ReadAllText(file).Contains(
                "IStringLocalizer<SharedResource>",
                StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(repositoryRoot.FullName, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "every routable page must source user-facing copy from the shared localization catalog");
    }

    [Fact]
    public void WebCalendarViews_DistinguishLoadingErrorAndEmptyStates()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string pagesRoot = Path.Combine(repositoryRoot.FullName, "src", "Cardscape.Web", "Pages");
        string[] pages = ["Calendar.razor", "Planner.razor"];

        string[] violations = pages
            .Where(page =>
            {
                string source = File.ReadAllText(Path.Combine(pagesRoot, page));
                return !source.Contains("@if (loading)", StringComparison.Ordinal)
                    || !source.Contains("else if (error is not null)", StringComparison.Ordinal)
                    || !source.Contains("result.Error", StringComparison.Ordinal);
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "calendar experiences must not misrepresent transport failures as valid empty schedules");
    }

    [Fact]
    public void WebHome_DoesNotFetchDataItDoesNotRender()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string homePath = Path.Combine(repositoryRoot.FullName, "src", "Cardscape.Web", "Pages", "Home.razor");
        string source = File.ReadAllText(homePath);

        source.Should().NotContain("ApiClient", "the home page must not issue requests for data absent from its UI");
        source.Should().NotContain("OnInitializedAsync", "the static home experience requires no data-loading lifecycle");
    }

    [Fact]
    public void WebApiTokens_DistinguishesLoadFailureAndLoadsStatusesConcurrently()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string path = Path.Combine(repositoryRoot.FullName, "src", "Cardscape.Web", "Pages", "ApiTokens.razor");
        string source = File.ReadAllText(path);

        source.Should().Contain("else if (loadError is not null)");
        source.Should().Contain("Task.WhenAll(", "live rate-limit status requests must not form a serial N+1 waterfall");
        source.Should().NotContain("tokens = result.IsSuccess ? result.Value : []",
            "a transport failure is not a valid empty token collection");
    }

    [Fact]
    public void WebCollectionPages_DoNotRenderLoadFailuresAsEmptyState()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string pagesRoot = Path.Combine(repositoryRoot.FullName, "src", "Cardscape.Web", "Pages");
        string[] pages = ["Automation.razor", "BoardDashboard.razor", "Invitations.razor"];

        string[] violations = pages
            .Where(page =>
            {
                string source = File.ReadAllText(Path.Combine(pagesRoot, page));
                return !source.Contains("if (loading)", StringComparison.Ordinal)
                    || !source.Contains("result.Error ?? L[", StringComparison.Ordinal)
                    || source.Contains("result.IsSuccess ? result.Value : []", StringComparison.Ordinal);
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "collection pages must distinguish a failed request from a successful empty result");
    }

    [Fact]
    public void WebIntegrationCollections_PreserveLoadFailures()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string pagesRoot = Path.Combine(repositoryRoot.FullName, "src", "Cardscape.Web", "Pages");
        string[] pages = ["Webhooks.razor", "WorkspaceEmail.razor", "WorkspaceScim.razor", "Workspaces.razor"];

        string[] violations = pages
            .Where(page =>
            {
                string source = File.ReadAllText(Path.Combine(pagesRoot, page));
                return !source.Contains("loadError is not null", StringComparison.Ordinal)
                    || !source.Contains("result.Error ?? L[", StringComparison.Ordinal)
                    || source.Contains("result.IsSuccess ? result.Value : []", StringComparison.Ordinal);
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "integration-backed collections must preserve a failed load separately from valid empty state");
    }

    [Fact]
    public void WebInbox_PreservesPrimaryAndAuxiliaryRequestOutcomes()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string path = Path.Combine(repositoryRoot.FullName, "src", "Cardscape.Web", "Pages", "Inbox.razor");
        string source = File.ReadAllText(path);

        source.Should().Contain("Task.WhenAll(countTask, listTask)");
        source.Should().Contain("loadError = list.Error ?? L[");
        source.Should().Contain("error = count.Error ?? L[");
        source.Should().NotContain("notifications = list.IsSuccess ? list.Value : []");
    }

    [Fact]
    public void WebWorkspaceMembers_PreservesIndependentLoadOutcomes()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string path = Path.Combine(repositoryRoot.FullName, "src", "Cardscape.Web", "Pages", "WorkspaceMembers.razor");
        string source = File.ReadAllText(path);

        source.Should().Contain("workspaceError = workspaceResult.IsSuccess");
        source.Should().Contain("membersError = membersResult.IsSuccess");
        source.Should().Contain("invitationsError = invitationsResult.IsSuccess");
        source.Should().NotContain("membersResult.IsSuccess ? membersResult.Value : []");
        source.Should().NotContain("invitationsResult.IsSuccess ? invitationsResult.Value : []");
    }

    [Fact]
    public void WebPages_DoNotCoerceFailedApiCollectionsToEmpty()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string pagesRoot = Path.Combine(repositoryRoot.FullName, "src", "Cardscape.Web", "Pages");

        string[] violations = Directory.GetFiles(pagesRoot, "*.razor", SearchOption.AllDirectories)
            .Where(path => Regex.IsMatch(
                File.ReadAllText(path),
                @"IsSuccess\s*\?[^;\r\n]+:\s*\[\]",
                RegexOptions.CultureInvariant))
            .Select(path => Path.GetRelativePath(repositoryRoot.FullName, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "API collection failures must remain distinguishable from successful empty responses");
    }

    [Fact]
    public void WebAutomation_UsesLocalizedVisibleCopy()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string path = Path.Combine(repositoryRoot.FullName, "src", "Cardscape.Web", "Pages", "Automation.razor");
        string source = File.ReadAllText(path);

        string[] forbiddenLiterals =
        [
            "Text=\"New rule\"", "Text=\"Disable\"", "Text=\"Enable\"",
            "Text=\"Delete\"", "PageHeader Title=\"Automation\""
        ];

        forbiddenLiterals.Where(source.Contains).Should().BeEmpty(
            "automation controls and headings must follow the active culture");
        source.Should().Contain("L[\"AutomationTriggerMoved\"]");
        source.Should().Contain("L[\"AutomationActionMove\"]");
    }

    [Fact]
    public void WebApiTokens_UsesLocalizedVisibleCopyAndCultureAwareDates()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string path = Path.Combine(repositoryRoot.FullName, "src", "Cardscape.Web", "Pages", "ApiTokens.razor");
        string source = File.ReadAllText(path);

        string[] forbiddenLiterals =
        [
            "PageHeader Title=\"API tokens\"", "Text=\"New token\"",
            "Text=\"Token created\"", "Text=\"Revoke\"", "yyyy-MM-dd"
        ];

        forbiddenLiterals.Where(source.Contains).Should().BeEmpty(
            "credential-management copy and dates must follow the active culture");
        source.Should().Contain("L[\"ApiTokensCreatedAt\"");
        source.Should().Contain("LocalDateTime.ToString(\"d\")");
    }

    [Fact]
    public void WebRazorComponents_DoNotUseUnobservableAsyncCallbacks()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string webRoot = Path.Combine(repositoryRoot.FullName, "src", "Cardscape.Web");
        Regex unobservableAsync = new(
            @"\basync\s+void\b|\.Elapsed\s*\+=\s*async\b|\.ContinueWith\s*\(",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        string[] violations = Directory.GetFiles(webRoot, "*.*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(file => unobservableAsync.IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(repositoryRoot.FullName, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "component callbacks must return observable Tasks and propagate disposal cancellation");
    }

    [Fact]
    public void WebRadzenForms_ValidateRequiredInputs()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string pagesRoot = Path.Combine(repositoryRoot.FullName, "src", "Cardscape.Web", "Pages");
        Dictionary<string, string[]> requiredInputs = new(StringComparer.Ordinal)
        {
            ["ApiTokens.razor"] = ["token-name"],
            ["Automation.razor"] = ["rule-name"],
            ["BoardDetail.razor"] = ["list-name", "board-name"],
            ["Boards.razor"] = ["b-name"],
            ["CardDetail.razor"] = ["comment-body"],
            ["CustomFields.razor"] = ["cf-name", "cf-options"],
            ["ForgotPassword.razor"] = ["email"],
            ["Login.razor"] = ["email", "password", "totp"],
            ["Register.razor"] = ["displayName", "email", "password", "confirmPassword"],
            ["ResetPassword.razor"] = ["token", "password", "confirmPassword"],
            ["SettingsOAuthApps.razor"] = ["name", "redirect", "scopes"],
            ["SettingsTwoFactor.razor"] = ["confirmation-code"],
            ["WorkspaceEmail.razor"] = ["email-address", "email-label", "email-list"],
            ["WorkspaceGitHub.razor"] = ["gh-board", "gh-repo", "gh-events", "gh-issue-card", "gh-issue-repo", "gh-issue-title"],
            ["WorkspaceMembers.razor"] = ["invite-email"],
            ["WorkspaceSaml.razor"] = ["s-slug", "s-name", "s-idp", "s-meta", "s-sp"],
            ["WorkspaceScim.razor"] = ["scim-name"],
            ["WorkspaceSlack.razor"] = ["slack-team-id", "slack-team-name", "slack-bot-token"],
            ["Workspaces.razor"] = ["ws-name"]
        };

        string[] violations = requiredInputs
            .SelectMany(entry => entry.Value.Select(component => (entry.Key, Component: component)))
            .Where(input => !File.ReadAllText(Path.Combine(pagesRoot, input.Key)).Contains(
                $"<RadzenRequiredValidator Component=\"{input.Component}\"",
                StringComparison.Ordinal))
            .Select(input => $"{input.Key}:{input.Component}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "every command input required by the application must provide immediate Radzen validation");
    }

    [Fact]
    public void WebRadzenDataGrids_DeclarePagingPolicy()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string webRoot = Path.Combine(repositoryRoot.FullName, "src", "Cardscape.Web");
        Regex dataGrid = new(
            "<RadzenDataGrid\\b(?<attributes>(?:\"[^\"]*\"|[^>])*)>",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        string[] violations = Directory.GetFiles(webRoot, "*.razor", SearchOption.AllDirectories)
            .SelectMany(file => dataGrid.Matches(File.ReadAllText(file))
                .Where(match => !match.Groups["attributes"].Value.Contains("AllowPaging=", StringComparison.Ordinal))
                .Select(_ => Path.GetRelativePath(repositoryRoot.FullName, file)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "every data grid must explicitly opt into paging or document its bounded data set by opting out");
    }

    [Fact]
    public void ApiEndpoints_DoNotConstructProblemDetailsDirectly()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string endpointsRoot = Path.Combine(
            repositoryRoot.FullName, "src", "Cardscape.Api", "Endpoints");

        string[] violations = Directory.GetFiles(endpointsRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("Results.Problem(", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(repositoryRoot.FullName, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "REST errors must use DomainErrorResults or ApiProblemResults so RFC 7807 fields and stable codes cannot drift");
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
