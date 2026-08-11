using Cardscape.Application.Abstractions.Security;
using Cardscape.Seeder.Configuration;
using Cardscape.Seeder.Reporting;
using Cardscape.Seeder.Steps;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cardscape.Seeder.DependencyInjection;

public static class SeederServiceCollectionExtensions
{
    /// <summary>Registers the seeder pipeline. The seeder is
    /// feature-gated: <see cref="SeederOptions.Enabled"/> is read
    /// at request time so flipping the toggle in
    /// <c>appsettings.json</c> + restarting the API is enough to
    /// hide every seeder surface from the running app.</summary>
    public static IServiceCollection AddCardscapeSeeder(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<SeederOptions>()
            .Bind(configuration.GetSection(SeederOptions.SectionName));

        services.AddSingleton<SeedReport>();

        // Every step is singleton. They are stateless and the only
        // injected collaborator, IPasswordHasher, is also singleton.
        // Singleton steps share the lifetime of the
        // SeedRunner (also singleton) and the SeedReport
        // (singleton), so the seeder is safe to invoke from
        // multiple HTTP requests in sequence.
        services.AddSingleton<ISeedStep, UsersSeedStep>();
        services.AddSingleton<ISeedStep, WorkspacesSeedStep>();
        services.AddSingleton<ISeedStep, BoardsSeedStep>();
        services.AddSingleton<ISeedStep, BoardExtensionsSeedStep>();
        services.AddSingleton<ISeedStep, LabelsAndDashboardsSeedStep>();
        services.AddSingleton<ISeedStep, ListsAndCardsSeedStep>();
        services.AddSingleton<ISeedStep, EngagementSeedStep>();
        services.AddSingleton<ISeedStep, AttachmentsAndMirrorsSeedStep>();
        services.AddSingleton<ISeedStep, CustomFieldValuesAndAgingSeedStep>();
        services.AddSingleton<ISeedStep, NotificationsAndTokensSeedStep>();
        services.AddSingleton<ISeedStep, IntegrationsSeedStep>();
        services.AddSingleton<ISeedStep, EnterpriseAuthSeedStep>();
        services.AddSingleton<ISeedStep, WebhooksAndBackgroundSeedStep>();
        services.AddSingleton<SeedRunner>(sp => new SeedRunner(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IOptionsMonitor<SeederOptions>>(),
            sp.GetRequiredService<IPasswordHasher>(),
            sp.GetServices<ISeedStep>(),
            sp.GetRequiredService<SeedReport>(),
            sp.GetRequiredService<ILogger<SeedRunner>>()));

        return services;
    }
}
