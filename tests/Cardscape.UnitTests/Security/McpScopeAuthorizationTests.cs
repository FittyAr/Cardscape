using System.Security.Claims;
using Cardscape.Domain.Security;
using Cardscape.Mcp.Authorization;
using FluentAssertions;

namespace Cardscape.UnitTests.Security;

public sealed class McpScopeAuthorizationTests
{
    [Fact]
    public void Authorize_ReadOperationWithReadScope_AllowsOperation()
    {
        ClaimsPrincipal principal = CreatePrincipal("read");

        Action act = () => McpScopeAuthorization.Authorize(Scope.Read, "resources/read", principal);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("write")]
    [InlineData("READ")]
    public void Authorize_ReadOperationWithoutExactReadScope_DeniesOperation(string grantedScope)
    {
        ClaimsPrincipal principal = CreatePrincipal(grantedScope);

        Action act = () => McpScopeAuthorization.Authorize(Scope.Read, "resources/read", principal);

        act.Should().Throw<ModelContextProtocol.McpException>()
            .WithMessage($"*{McpScopeAuthorization.ForbiddenErrorCode}*'resources/read'*'read'*");
    }

    [Fact]
    public void Authorize_ReadOperationWithAnonymousPrincipal_DeniesOperation()
    {
        Action act = () => McpScopeAuthorization.Authorize(
            Scope.Read,
            "prompts/get",
            new ClaimsPrincipal());

        act.Should().Throw<ModelContextProtocol.McpException>()
            .WithMessage($"*{McpScopeAuthorization.ForbiddenErrorCode}*'prompts/get'*'read'*");
    }

    [Fact]
    public async Task AuthorizeAndInvokeAsync_DeniedSubscription_DoesNotReachHandler()
    {
        bool invoked = false;

        Func<Task> act = async () => await McpScopeAuthorization.AuthorizeAndInvokeAsync(
            Scope.Read,
            "resources/subscribe",
            CreatePrincipal("write"),
            () =>
            {
                invoked = true;
                return ValueTask.FromResult(0);
            });

        await act.Should().ThrowAsync<ModelContextProtocol.McpException>()
            .WithMessage($"*{McpScopeAuthorization.ForbiddenErrorCode}*");
        invoked.Should().BeFalse();
    }

    private static ClaimsPrincipal CreatePrincipal(params string[] scopes)
    {
        IEnumerable<Claim> claims = scopes.Select(scope =>
            new Claim(McpScopeAuthorization.ScopeClaimType, scope));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "ApiToken"));
    }
}
