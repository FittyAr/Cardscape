using Cardscape.Api.Authentication;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Domain.Authentication.ExternalLogins;
using Cardscape.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;

namespace Cardscape.Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    private static void AddExternalAuthenticationProviders(
        IServiceCollection services,
        AuthenticationBuilder authentication,
        IConfiguration configuration)
    {
        string? googleClientId = configuration["Authentication:Google:ClientId"];
        string? googleClientSecret = configuration["Authentication:Google:ClientSecret"];
        if (!string.IsNullOrWhiteSpace(googleClientId)
            && !string.IsNullOrWhiteSpace(googleClientSecret))
        {
            authentication.AddGoogle(options =>
            {
                options.SignInScheme = ExternalCookieScheme;
                options.ClientId = googleClientId;
                options.ClientSecret = googleClientSecret;
                options.Scope.Add("email");
                options.Scope.Add("profile");
            });
        }

        string? microsoftClientId = configuration["Authentication:Microsoft:ClientId"];
        string? microsoftClientSecret = configuration["Authentication:Microsoft:ClientSecret"];
        if (!string.IsNullOrWhiteSpace(microsoftClientId)
            && !string.IsNullOrWhiteSpace(microsoftClientSecret))
        {
            authentication.AddMicrosoftAccount(options =>
            {
                options.SignInScheme = ExternalCookieScheme;
                options.ClientId = microsoftClientId;
                options.ClientSecret = microsoftClientSecret;
                options.Scope.Add("email");
                options.Scope.Add("profile");
            });
        }

        AddAppleAuthentication(services, authentication, configuration);
    }

    private static void AddAppleAuthentication(
        IServiceCollection services,
        AuthenticationBuilder authentication,
        IConfiguration configuration)
    {
        string? clientId = configuration["Authentication:Apple:ClientId"];
        string? teamId = configuration["Authentication:Apple:TeamId"];
        string? keyId = configuration["Authentication:Apple:KeyId"];
        string? privateKeyPem = configuration["Authentication:Apple:PrivateKeyPem"];
        if (string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(teamId)
            || string.IsNullOrWhiteSpace(keyId)
            || string.IsNullOrWhiteSpace(privateKeyPem))
        {
            return;
        }

        services.AddSingleton<IAppleClientSecretGenerator, AppleClientSecretGenerator>();
        authentication.AddOpenIdConnect(ExternalProvider.Apple.WireName(), options =>
        {
            options.SignInScheme = ExternalCookieScheme;
            options.Authority = "https://appleid.apple.com";
            options.ClientId = clientId;
            options.ClientSecret = "placeholder-replaced-on-redirect";
            options.CallbackPath = "/signin-apple";
            options.Scope.Add("openid");
            options.Scope.Add("email");
            options.Scope.Add("name");
            options.ResponseType = "code";
            options.UsePkce = true;
            options.SaveTokens = true;
            options.Events.OnRedirectToIdentityProvider = context =>
            {
                var generator = context.HttpContext.RequestServices
                    .GetRequiredService<IAppleClientSecretGenerator>();
                context.ProtocolMessage.ClientSecret = generator.GenerateClientSecret(
                    TimeSpan.FromDays(180));
                return Task.CompletedTask;
            };
        });
    }

    private static void AddFederationSchemes(AuthenticationBuilder authentication)
    {
        authentication.AddScheme<ScimAuthenticationOptions, ScimAuthenticationHandler>(
            ScimAuthenticationHandler.SchemeName,
            _ => { });
        authentication.AddScheme<Sustainsys.Saml2.AspNetCore2.Saml2Options, SamlAuthenticationHandler>(
            SamlAuthenticationHandler.SchemeName,
            _ => { });
    }
}
