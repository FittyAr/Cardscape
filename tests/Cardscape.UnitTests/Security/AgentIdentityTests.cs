using System.Security.Claims;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Members;
using Cardscape.Domain.Security;

namespace Cardscape.UnitTests.Security;

/// <summary>The agent-key principal and scope rules of ADR 0015.</summary>
public sealed class AgentIdentityTests
{
    private static readonly ApiTokenValidation Validation = new(
        new UserId(Guid.NewGuid()),
        new ApiTokenId(Guid.NewGuid()),
        ["read", "cards:write", "write"],
        "Claude Desktop");

    [Fact]
    public void CreatePrincipal_IsTheOwner_MarkedAsTheirAgent()
    {
        ClaimsPrincipal principal = AgentIdentity.CreatePrincipal("ApiToken", Validation, writesAllowed: true);

        principal.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(Validation.UserId.Value.ToString());
        principal.FindFirst(AgentIdentity.TokenIdClaim)!.Value.Should().Be(Validation.TokenId.Value.ToString());
        principal.FindFirst(AgentIdentity.AgentNameClaim)!.Value.Should().Be("Claude Desktop");
        AgentIdentity.GrantedScopes(principal).Should().BeEquivalentTo([Scope.Read, Scope.CardsWrite, Scope.Write]);
    }

    [Fact]
    public void CreatePrincipal_WhenTheAdministratorForbidsWrites_KeepsOnlyRead()
    {
        ClaimsPrincipal principal = AgentIdentity.CreatePrincipal("ApiToken", Validation, writesAllowed: false);

        AgentIdentity.GrantedScopes(principal).Should().Equal(Scope.Read);
    }

    [Fact]
    public void ActingAgent_IsNullForAPersonAndNamedForAnAgent()
    {
        ICurrentUser person = new CurrentUser(new FixedPrincipal(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "Bearer"))));
        ICurrentUser agent = new CurrentUser(new FixedPrincipal(
            AgentIdentity.CreatePrincipal("ApiToken", Validation, writesAllowed: true)));

        person.ActingAgent().Should().BeNull();
        agent.ActingAgent().Should().Be(new ActingAgent(Validation.TokenId.Value, "Claude Desktop"));
    }

    [Theory]
    [InlineData(Scope.CardsWrite, Scope.CardsWrite, true)]
    [InlineData(Scope.Write, Scope.BoardsWrite, true)]
    [InlineData(Scope.Write, Scope.Read, false)]
    [InlineData(Scope.CardsWrite, Scope.CommentsWrite, false)]
    [InlineData(Scope.Read, Scope.CardsWrite, false)]
    public void Grants_WriteImpliesEveryCapability_ButNotRead(Scope granted, Scope required, bool expected) =>
        ScopeExtensions.Grants([granted], required).Should().Be(expected);

    [Fact]
    public void Scopes_RoundTripTheirWireForm()
    {
        ApiTokenScopes scopes = ApiTokenScopes.Create(["read", "cards:write", "comments:write", "boards:write", "workspace:write"]).Value;

        ApiTokenScopes.Create(scopes.ToString().Split(';')).Value.Values.Should().BeEquivalentTo(scopes.Values);
        ApiTokenScopes.Create(["cards:delete"]).IsFailure.Should().BeTrue();
    }

    private sealed class FixedPrincipal(ClaimsPrincipal principal) : ICurrentUserAccessor
    {
        public ClaimsPrincipal? GetCurrentPrincipal() => principal;
    }
}
