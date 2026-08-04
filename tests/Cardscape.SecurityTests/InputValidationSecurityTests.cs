using System.Net;
using System.Net.Http.Json;
using Cardscape.SecurityTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace Cardscape.SecurityTests;

/// <summary>
/// OWASP A03:2021 — Injection (SQL, NoSQL, LDAP, OS
/// command, etc.). These tests pin the contract that
/// user input is parameterised, not concatenated, when
/// the value is used in a query, a file path, or a
/// shell command.
/// </summary>
[Collection(SecurityApi.Name)]
public sealed class InputValidationSecurityTests
{
    private readonly SecurityTestsWebApplicationFactory _factory;
    public InputValidationSecurityTests(SecurityTestsWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("' OR 1=1 --")]
    [InlineData("'; DROP TABLE users; --")]
    [InlineData("' UNION SELECT * FROM api_tokens --")]
    [InlineData("1' OR '1'='1")]
    public async Task Register_With_SQLi_In_Email_Is_Rejected(string sqli)
    {
        HttpClient client = _factory.CreateApiClient();
        var body = new
        {
            email = sqli,
            displayName = "X",
            password = "Password123!"
        };
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/auth/register", body, TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            $"the email validator should reject the SQLi attempt; " +
            $"a 500 would mean the validator bypassed and the query " +
            $"reached the SQL parser with the injection");
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\Windows\\System32\\config\\SAM")]
    [InlineData("C:\\Windows\\System32\\drivers\\etc\\hosts")]
    [InlineData("/etc/shadow")]
    public async Task Register_With_PathTraversal_In_DisplayName_Does_Not_Crash_Server(string traversal)
    {
        // The display name validator allows the
        // string through (a path-looking display
        // name is not a security issue in itself —
        // the display name is never used as a file
        // path). The regression we want to catch
        // is a server-side crash: a refactor that
        // uses the display name as part of a file
        // path (e.g. for an avatar upload) would
        // either succeed (real traversal) or 500
        // (Path.Combine threw on the relative
        // segments). The test pins "no 500" so a
        // future regression that wires display
        // names into a file path fires here.
        HttpClient client = _factory.CreateApiClient();
        var body = new
        {
            email = $"path-{Guid.NewGuid():N}@cardscape.local",
            displayName = traversal,
            password = "Password123!"
        };
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/auth/register", body, TestContext.Current.CancellationToken);
        ((int)resp.StatusCode).Should().BeLessThan(500,
            "the server must not crash on path-traversal-like display names; " +
            "if it does, a refactor is using the display name as a file path");
    }

    [Fact]
    public async Task Register_With_Extremely_Long_Email_Is_Rejected()
    {
        HttpClient client = _factory.CreateApiClient();
        string longLocal = new('a', 300);
        var body = new
        {
            email = $"{longLocal}@cardscape.local",
            displayName = "Long",
            password = "Password123!"
        };
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/auth/register", body, TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "the email validator should reject the over-long input");
    }

    [Fact]
    public async Task Register_With_Control_Characters_In_DisplayName_Is_Rejected()
    {
        HttpClient client = _factory.CreateApiClient();
        var body = new
        {
            email = $"ctrl-{Guid.NewGuid():N}@cardscape.local",
            displayName = "evil\u001b[31mred\u001b[0m",
            password = "Password123!"
        };
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/auth/register", body, TestContext.Current.CancellationToken);
        ((int)resp.StatusCode).Should().NotBe(200,
            "the display name validator should reject ANSI escape sequences");
    }
}
