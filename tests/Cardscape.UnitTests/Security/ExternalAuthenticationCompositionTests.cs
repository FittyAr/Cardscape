using Cardscape.Api.Endpoints.Auth;
using Cardscape.Api.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Cardscape.UnitTests.Security;

public sealed class ExternalAuthenticationCompositionTests
{
    [Fact]
    public void AddApiAuthentication_ConfiguresExternalProvidersToUseEphemeralCookie()
    {
        using ServiceProvider provider = BuildServices(AllProvidersConfiguration());

        AuthenticationOptions defaults = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        CookieAuthenticationOptions cookie = provider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(ServiceCollectionExtensions.ExternalCookieScheme);
        GoogleOptions google = provider.GetRequiredService<IOptionsMonitor<GoogleOptions>>().Get("google");
        MicrosoftAccountOptions microsoft = provider
            .GetRequiredService<IOptionsMonitor<MicrosoftAccountOptions>>()
            .Get("microsoft");
        OpenIdConnectOptions apple = provider
            .GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get("apple");

        defaults.DefaultAuthenticateScheme.Should().Be(ServiceCollectionExtensions.BearerPolicyScheme);
        defaults.DefaultChallengeScheme.Should().Be(ServiceCollectionExtensions.BearerPolicyScheme);
        defaults.DefaultSignInScheme.Should().Be(ServiceCollectionExtensions.ExternalCookieScheme);
        cookie.Cookie.Name.Should().Be("cardscape.external");
        cookie.Cookie.HttpOnly.Should().BeTrue();
        cookie.ExpireTimeSpan.Should().Be(TimeSpan.FromMinutes(10));
        cookie.SlidingExpiration.Should().BeFalse();
        google.SignInScheme.Should().Be(ServiceCollectionExtensions.ExternalCookieScheme);
        microsoft.SignInScheme.Should().Be(ServiceCollectionExtensions.ExternalCookieScheme);
        apple.SignInScheme.Should().Be(ServiceCollectionExtensions.ExternalCookieScheme);
        apple.CallbackPath.Value.Should().Be("/signin-apple");
    }

    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("https://evil.example", "/")]
    [InlineData("//evil.example/path", "/")]
    [InlineData("/\\evil.example", "/")]
    [InlineData("/settings/security?tab=providers", "/settings/security?tab=providers")]
    public void NormalizeReturnUrl_AllowsOnlyLocalPaths(string? candidate, string expected)
    {
        ExternalLoginEndpoints.NormalizeReturnUrl(candidate).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "google", false)]
    [InlineData("microsoft", "google", false)]
    [InlineData("Google", "google", false)]
    [InlineData("google", "google", true)]
    public void IsExpectedProvider_RequiresExactProtectedProvider(
        string? actualProvider,
        string expectedProvider,
        bool expected)
    {
        var properties = new AuthenticationProperties();
        if (actualProvider is not null)
        {
            properties.Items["cardscape.provider"] = actualProvider;
        }

        ExternalLoginEndpoints.IsExpectedProvider(properties, expectedProvider).Should().Be(expected);
    }

    private static ServiceProvider BuildServices(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddApiAuthentication(configuration);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static IConfiguration AllProvidersConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["Cors:AllowedOrigins:0"] = "https://cardscape.example",
            ["Jwt:SigningKey"] = "external-auth-tests-signing-key-at-least-32-bytes",
            ["Authentication:Google:ClientId"] = "google-client",
            ["Authentication:Google:ClientSecret"] = "google-secret",
            ["Authentication:Microsoft:ClientId"] = "microsoft-client",
            ["Authentication:Microsoft:ClientSecret"] = "microsoft-secret",
            ["Authentication:Apple:ClientId"] = "apple-client",
            ["Authentication:Apple:TeamId"] = "apple-team",
            ["Authentication:Apple:KeyId"] = "apple-key",
            ["Authentication:Apple:PrivateKeyPem"] = "test-private-key"
        };
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
