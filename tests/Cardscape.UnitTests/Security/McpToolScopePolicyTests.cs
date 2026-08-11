using System.Security.Claims;
using Cardscape.Mcp.Authorization;
using FluentAssertions;

namespace Cardscape.UnitTests.Security;

public sealed class McpToolScopePolicyTests
{
    [Theory]
    [InlineData("boards_get", "read")]
    [InlineData("cards_create", "write")]
    public void Authorize_WithRequiredScope_AllowsTool(string toolName, string scope)
    {
        ClaimsPrincipal principal = CreatePrincipal(scope);

        Action act = () => McpToolScopePolicy.Authorize(toolName, principal);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("boards_get", "write", "read")]
    [InlineData("cards_create", "read", "write")]
    public void Authorize_WithoutRequiredScope_DeniesTool(
        string toolName,
        string grantedScope,
        string requiredScope)
    {
        ClaimsPrincipal principal = CreatePrincipal(grantedScope);

        Action act = () => McpToolScopePolicy.Authorize(toolName, principal);

        act.Should().Throw<ModelContextProtocol.McpException>()
            .WithMessage($"*{McpToolScopePolicy.ForbiddenErrorCode}*'{toolName}'*'{requiredScope}'*");
    }

    [Fact]
    public void Authorize_WithAnonymousPrincipal_DeniesTool()
    {
        Action act = () => McpToolScopePolicy.Authorize("boards_get", new ClaimsPrincipal());

        act.Should().Throw<ModelContextProtocol.McpException>()
            .WithMessage($"*{McpToolScopePolicy.ForbiddenErrorCode}*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unregistered_tool")]
    public void Authorize_WithUnclassifiedTool_DeniesByDefault(string? toolName)
    {
        ClaimsPrincipal principal = CreatePrincipal("read", "write");

        Action act = () => McpToolScopePolicy.Authorize(toolName, principal);

        act.Should().Throw<ModelContextProtocol.McpException>()
            .WithMessage($"*{McpToolScopePolicy.UnclassifiedErrorCode}*");
    }

    [Fact]
    public void Authorize_WithDifferentlyCasedScope_DeniesTool()
    {
        ClaimsPrincipal principal = CreatePrincipal("READ");

        Action act = () => McpToolScopePolicy.Authorize("boards_get", principal);

        act.Should().Throw<ModelContextProtocol.McpException>()
            .WithMessage($"*{McpToolScopePolicy.ForbiddenErrorCode}*");
    }

    [Fact]
    public async Task AuthorizeAndInvokeAsync_WithRequiredScope_InvokesNextOnce()
    {
        int invocationCount = 0;

        int result = await McpToolScopePolicy.AuthorizeAndInvokeAsync(
            "boards_get",
            CreatePrincipal("read"),
            () =>
            {
                invocationCount++;
                return ValueTask.FromResult(42);
            });

        result.Should().Be(42);
        invocationCount.Should().Be(1);
    }

    [Fact]
    public async Task AuthorizeAndInvokeAsync_WithoutRequiredScope_DoesNotInvokeNext()
    {
        bool invoked = false;

        Func<Task> act = async () => await McpToolScopePolicy.AuthorizeAndInvokeAsync(
            "cards_create",
            CreatePrincipal("read"),
            () =>
            {
                invoked = true;
                return ValueTask.FromResult(42);
            });

        await act.Should().ThrowAsync<ModelContextProtocol.McpException>()
            .WithMessage($"*{McpToolScopePolicy.ForbiddenErrorCode}*");
        invoked.Should().BeFalse();
    }

    private static ClaimsPrincipal CreatePrincipal(params string[] scopes)
    {
        IEnumerable<Claim> claims = scopes.Select(scope =>
            new Claim(McpToolScopePolicy.ScopeClaimType, scope));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "ApiToken"));
    }
}
