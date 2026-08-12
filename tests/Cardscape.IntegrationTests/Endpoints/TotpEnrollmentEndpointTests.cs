using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;
using OtpNet;
using Xunit;

namespace Cardscape.IntegrationTests.Endpoints;

[Collection(CardscapeApi.Name)]
public sealed class TotpEnrollmentEndpointTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public TotpEnrollmentEndpointTests(CardscapeWebApplicationFactory factory) =>
        _factory = factory;

    [Fact]
    public async Task Enroll_BeforeConfirmation_RemainsPendingAndRecoveryCodeCannotActivateIt()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        TotpEnrollmentResponse enrollment = await EnrollAsync(client);

        TotpStatusResponse pending = await GetStatusAsync(client);
        pending.IsEnrolled.Should().BeFalse();
        pending.HasPendingEnrollment.Should().BeTrue();
        pending.EnrolledAt.Should().BeNull();
        pending.RemainingRecoveryCodes.Should().Be(0);

        HttpResponseMessage recoveryAttempt = await client.PostAsJsonAsync(
            "api/auth/2fa/confirm",
            new { code = enrollment.RecoveryCodes[0] },
            TestContext.Current.CancellationToken);
        recoveryAttempt.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        HttpResponseMessage recoveryVerification = await client.PostAsJsonAsync(
            "api/auth/2fa/verify",
            new { code = enrollment.RecoveryCodes[0] },
            TestContext.Current.CancellationToken);
        recoveryVerification.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        TotpStatusResponse unchanged = await GetStatusAsync(client);
        unchanged.IsEnrolled.Should().BeFalse();
        unchanged.HasPendingEnrollment.Should().BeTrue();
    }

    [Fact]
    public async Task Confirm_WithCurrentAuthenticatorCode_ActivatesEnrollmentExactlyOnce()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        TotpEnrollmentResponse enrollment = await EnrollAsync(client);
        string code = new Totp(Base32Encoding.ToBytes(enrollment.Secret)).ComputeTotp();

        HttpResponseMessage confirmation = await client.PostAsJsonAsync(
            "api/auth/2fa/confirm", new { code }, TestContext.Current.CancellationToken);
        confirmation.StatusCode.Should().Be(HttpStatusCode.NoContent);

        TotpStatusResponse active = await GetStatusAsync(client);
        active.IsEnrolled.Should().BeTrue();
        active.HasPendingEnrollment.Should().BeFalse();
        active.EnrolledAt.Should().NotBeNull();
        active.RemainingRecoveryCodes.Should().Be(10);

        HttpResponseMessage repeated = await client.PostAsJsonAsync(
            "api/auth/2fa/confirm", new { code }, TestContext.Current.CancellationToken);
        repeated.StatusCode.Should().Be(HttpStatusCode.Conflict);

        HttpResponseMessage reenrollment = await client.PostAsync(
            "api/auth/2fa/enroll", null, TestContext.Current.CancellationToken);
        reenrollment.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetStatusAsync(client)).IsEnrolled.Should().BeTrue();
    }

    [Fact]
    public async Task Enroll_WhenPreviousSetupIsPending_RotatesSecretAndRecoveryCodes()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        TotpEnrollmentResponse first = await EnrollAsync(client);

        TotpEnrollmentResponse second = await EnrollAsync(client);

        second.CredentialId.Should().NotBe(first.CredentialId);
        second.Secret.Should().NotBe(first.Secret);
        second.RecoveryCodes.Should().NotEqual(first.RecoveryCodes);
        (await GetStatusAsync(client)).HasPendingEnrollment.Should().BeTrue();
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        var register = new RegisterRequest(
            $"totp-{Guid.NewGuid():N}@cardscape.local", "TOTP User", "Password123!");
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/auth/register", register, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        AuthResponse auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(
            TestJson.Options, TestContext.Current.CancellationToken))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<TotpEnrollmentResponse> EnrollAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsync(
            "api/auth/2fa/enroll", null, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TotpEnrollmentResponse>(
            TestJson.Options, TestContext.Current.CancellationToken))!;
    }

    private static async Task<TotpStatusResponse> GetStatusAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<TotpStatusResponse>(
            "api/auth/2fa/status", TestJson.Options, TestContext.Current.CancellationToken))!;

    private sealed record TotpEnrollmentResponse(
        Guid CredentialId,
        string Secret,
        string[] RecoveryCodes);

    private sealed record TotpStatusResponse(
        bool IsEnrolled,
        bool HasPendingEnrollment,
        DateTimeOffset? EnrolledAt,
        int RemainingRecoveryCodes);
}
