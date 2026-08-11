using Cardscape.Api.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cardscape.UnitTests.Security;

public sealed class JwtApiConfigurationTests
{
    [Fact]
    public void AddApiAuthentication_WithoutProductionSigningKey_RejectsStartupConfiguration()
    {
        IConfiguration configuration = BuildConfiguration(signingKey: null);
        var services = new ServiceCollection();

        Action act = () => services.AddApiAuthentication(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:SigningKey is required*");
    }

    [Fact]
    public void AddApiAuthentication_WithShortSigningKey_RejectsStartupConfiguration()
    {
        IConfiguration configuration = BuildConfiguration("too-short");
        var services = new ServiceCollection();

        Action act = () => services.AddApiAuthentication(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least 32 bytes*");
    }

    private static IConfiguration BuildConfiguration(string? signingKey)
    {
        var values = new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["Cors:AllowedOrigins:0"] = "https://cardscape.example",
            ["Jwt:SigningKey"] = signingKey
        };
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
