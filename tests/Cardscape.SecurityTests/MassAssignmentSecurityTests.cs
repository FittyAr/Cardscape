using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.SecurityTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace Cardscape.SecurityTests;

/// <summary>
/// OWASP A04:2021 — Insecure Design / Mass Assignment.
/// These tests pin the contract that the server-side
/// command / query types ignore fields the client sends
/// in the request body that are not part of the
/// documented contract. A future refactor that wires
/// user input straight to a domain model (e.g. with
/// <c>JsonSerializer.Deserialize&lt;User&gt;</c>) would
/// let a malicious client set the <c>IsAdmin</c> flag
/// or the <c>Email</c> field on someone else's user
/// record; these tests catch that regression before
/// it ships.
/// </summary>
[Collection(SecurityApi.Name)]
public sealed class MassAssignmentSecurityTests
{
    private readonly SecurityTestsWebApplicationFactory _factory;
    public MassAssignmentSecurityTests(SecurityTestsWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_With_AdminFlag_In_Body_Is_Ignored()
    {
        HttpClient client = _factory.CreateApiClient();

        string smuggleEmail = $"smuggle-{Guid.NewGuid():N}@cardscape.local";
        var smuggle = new
        {
            email = smuggleEmail,
            displayName = "Smuggle",
            password = "Password123!",
            isAdmin = true,
            role = "admin"
        };

        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/auth/register", smuggle, TestContext.Current.CancellationToken);

        resp.IsSuccessStatusCode.Should().BeTrue(
            $"the register endpoint accepted the body but {resp.StatusCode} " +
            $"is acceptable; the regression is `isAdmin` getting applied");
        string body = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotContain("\"isAdmin\":true",
            "the response body must not echo the smuggle");
    }

    [Fact]
    public async Task Register_With_Nested_AdminField_In_Body_Is_Ignored()
    {
        HttpClient client = _factory.CreateApiClient();
        var smuggle = new
        {
            user = new
            {
                email = $"wrap-{Guid.NewGuid():N}@cardscape.local",
                displayName = "Wrap",
                password = "Password123!",
                isAdmin = true
            }
        };
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/auth/register", smuggle, TestContext.Current.CancellationToken);
        ((int)resp.StatusCode).Should().BeLessThan(500,
            "the server crashed deserialising the wrapper; " +
            "this means the endpoint takes `object` / `dynamic` somewhere");
    }

    [Fact]
    public async Task Login_With_PasswordOverride_Field_Is_Ignored()
    {
        HttpClient client = _factory.CreateApiClient();
        var email = $"login-{Guid.NewGuid():N}@cardscape.local";
        await client.PostAsJsonAsync("api/auth/register",
            new { email, displayName = "Login", password = "Password123!" },
            TestContext.Current.CancellationToken);

        var smuggle = new
        {
            email,
            password = "Password123!",
            passwordHash = "EXTERNAL::bypass-attempt"
        };
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/auth/login", smuggle, TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "the legit credentials work; the smuggle must not change the outcome");
    }
}
