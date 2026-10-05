using Cardscape.Application.Abstractions.Settings;
using Cardscape.Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cardscape.UnitTests.Infrastructure;

public sealed class SystemSettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IConfiguration _config;

    public SystemSettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"cardscape-settings-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var configValues = new Dictionary<string, string?>
        {
            ["Cardscape:DataRoot"] = _tempDir,
            ["Database:Provider"] = "PostgreSQL",
            ["ASPNETCORE_ENVIRONMENT"] = "Testing",
            ["Storage:LocalRoot"] = "TestStorage",
            ["Jwt:AccessTokenMinutes"] = "45"
        };


        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsDefaultConfiguredValues()
    {
        using var service = new SystemSettingsService(_config, NullLogger<SystemSettingsService>.Instance);

        SystemSettingsDto settings = await service.GetSettingsAsync(TestContext.Current.CancellationToken);

        settings.InstanceTitle.Should().Be("Cardscape");
        settings.AllowPublicRegistration.Should().BeTrue();
        settings.DatabaseProvider.Should().Be("PostgreSQL");
        settings.Environment.Should().Be("Testing");
        settings.JwtAccessTokenMinutes.Should().Be(45);
    }

    [Fact]
    public async Task UpdateSettingsAsync_PersistsAndReflectsUpdatedValues()
    {
        using var service = new SystemSettingsService(_config, NullLogger<SystemSettingsService>.Instance);

        var updateReq = new UpdateSystemSettingsRequest(
            InstanceTitle: "Empresa XYZ",
            AllowPublicRegistration: false,
            DefaultLanguage: "es",
            JwtAccessTokenMinutes: 120);

        SystemSettingsDto updated = await service.UpdateSettingsAsync(updateReq, "admin@test.com", TestContext.Current.CancellationToken);

        updated.InstanceTitle.Should().Be("Empresa XYZ");
        updated.AllowPublicRegistration.Should().BeFalse();
        updated.DefaultLanguage.Should().Be("es");
        updated.JwtAccessTokenMinutes.Should().Be(120);

        bool isAllowed = await service.IsPublicRegistrationAllowedAsync(TestContext.Current.CancellationToken);
        isAllowed.Should().BeFalse();
    }


    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}
