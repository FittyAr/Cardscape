using Cardscape.Application.Abstractions;
using Cardscape.Infrastructure.DependencyInjection;
using Cardscape.Infrastructure.Hosting;
using Cardscape.Tests.Common.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Cardscape.UnitTests.Hosting;

public sealed class InfrastructureOptionsValidationTests
{
    [Fact]
    public async Task RetentionAndRevocationSweeperOptions_WithDefaults_PassStartupValidation()
    {
        using IHost host = CreateHost(new Dictionary<string, string?>());

        Func<Task> act = () => host.StartAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();

        RetentionSettingsOptions retention = host.Services
            .GetRequiredService<IOptions<RetentionSettingsOptions>>().Value;
        retention.SweepIntervalSeconds.Should().Be(21_600);
        retention.UserGracePeriodDays.Should().Be(30);
        retention.ActivityRetentionDays.Should().Be(365);
        retention.AuditRetentionDays.Should().Be(730);
        retention.BatchSize.Should().Be(100);

        RevocationSweeperOptions revocation = host.Services
            .GetRequiredService<IOptions<RevocationSweeperOptions>>().Value;
        revocation.SweepInterval.Should().Be(TimeSpan.FromMinutes(30));
        revocation.InitialDelay.Should().Be(TimeSpan.FromMinutes(1));
        revocation.Enabled.Should().BeTrue();

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("Retention:SweepIntervalSeconds", "0")]
    [InlineData("Retention:UserGracePeriodDays", "-1")]
    [InlineData("Retention:ActivityRetentionDays", "0")]
    [InlineData("Retention:AuditRetentionDays", "0")]
    [InlineData("Retention:BatchSize", "0")]
    public async Task RetentionOptions_WithInvalidValue_FailStartupValidation(string key, string value)
    {
        using IHost host = CreateHost(new Dictionary<string, string?> { [key] = value });

        Func<Task> act = () => host.StartAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OptionsValidationException>()
            .Where(exception => exception.OptionsType == typeof(RetentionSettingsOptions));
    }

    [Theory]
    [InlineData("RevocationSweeper:SweepInterval", "00:00:00")]
    [InlineData("RevocationSweeper:SweepInterval", "-00:00:01")]
    [InlineData("RevocationSweeper:InitialDelay", "-00:00:01")]
    public async Task RevocationSweeperOptions_WithInvalidValue_FailStartupValidation(string key, string value)
    {
        using IHost host = CreateHost(new Dictionary<string, string?> { [key] = value });

        Func<Task> act = () => host.StartAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OptionsValidationException>()
            .Where(exception => exception.OptionsType == typeof(RevocationSweeperOptions));
    }

    private static IHost CreateHost(IReadOnlyDictionary<string, string?> overrides)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        var configuration = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Data Source=:memory:",
            ["Database:Provider"] = "Sqlite",
            ["Jwt:SigningKey"] = "unit-tests-signing-key-please-override-32-characters"
        };
        foreach ((string key, string? value) in overrides)
        {
            configuration[key] = value;
        }

        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Services.AddSingleton<IClock>(new FakeClock());
        builder.Services.AddCardscapeInfrastructure(builder.Configuration);
        return builder.Build();
    }
}
