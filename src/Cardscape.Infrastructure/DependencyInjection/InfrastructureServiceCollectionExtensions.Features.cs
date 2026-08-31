using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Calendar;
using Cardscape.Application.Abstractions.Import;
using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Search;
using Cardscape.Application.Abstractions.Storage;
using Cardscape.Infrastructure.Ai;
using Cardscape.Infrastructure.Calendar;
using Cardscape.Infrastructure.Configuration;
using Cardscape.Infrastructure.Export;
using Cardscape.Infrastructure.Import;
using Cardscape.Infrastructure.Integrations;
using Cardscape.Infrastructure.Repositories;
using Cardscape.Infrastructure.Scim;
using Cardscape.Infrastructure.Search;
using Cardscape.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cardscape.Infrastructure.DependencyInjection;

public static partial class InfrastructureServiceCollectionExtensions
{
    private static void AddFeatureInfrastructure(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IImportService, KanbanImportService>();
        services.AddScoped<ISearchService, DatabaseSearchService>();

        string aiProvider = configuration["Ai:Provider"] ?? "OpenAiCompatible";
        services.Configure<AiProviderOptions>(configuration.GetSection("Ai"));
        if (!aiProvider.Equals("OpenAiCompatible", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unsupported Ai:Provider '{aiProvider}'. Only OpenAiCompatible is supported.");
        }

        string endpoint = configuration["Ai:Endpoint"] ?? "http://localhost:11434/";
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? endpointUri)
            || (!endpointUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !endpointUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Ai:Endpoint must be an absolute HTTP or HTTPS URL.");
        }

        services.AddHttpClient<IAiService, OpenAiCompatibleAiService>(client =>
        {
            client.BaseAddress = endpointUri;
            client.Timeout = TimeSpan.FromSeconds(60);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });

        string storageRoot = configuration["Storage:LocalRoot"]
            ?? Path.Combine(AppContext.BaseDirectory, "storage");
        services.AddSingleton<IStorageService>(_ => new LocalFileStorageService(storageRoot));
        services.AddSingleton<IDeploymentRegion, ConfigurationDeploymentRegion>();

        services.AddScoped<IGoogleCalendarConnectionRepository, GoogleCalendarConnectionRepository>();
        services.AddTransient<IGoogleCalendarSyncService, HttpGoogleCalendarSyncService>();
        services.AddHttpClient("google-oauth", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });
        services.AddHttpClient(nameof(IGoogleCalendarSyncService), client =>
        {
            client.BaseAddress = new Uri("https://www.googleapis.com/calendar/v3/");
            client.Timeout = TimeSpan.FromSeconds(15);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });

        services.AddScoped<IScimTokenRepository, ScimTokenRepository>();
        services.AddScoped<IScimService, ScimService>();
        services.AddScoped<ISamlConnectionRepository, SamlConnectionRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();

        services.AddScoped<ISlackWorkspaceRepository, SlackWorkspaceRepository>();
        services.AddScoped<ISlackChannelRepository, SlackChannelRepository>();
        services.AddHttpClient<ISlackNotificationService, HttpSlackNotificationService>(client =>
        {
            client.BaseAddress = new Uri("https://slack.com/api/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddScoped<IGitHubRepoLinkRepository, GitHubRepoLinkRepository>();
        services.AddScoped<IGitHubPullRequestLinkRepository, GitHubPullRequestLinkRepository>();
        services.AddHttpClient<IGitHubService, HttpGitHubService>(client =>
        {
            client.BaseAddress = new Uri("https://api.github.com");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddScoped<IInboundEmailAddressRepository, InboundEmailAddressRepository>();
        services.AddScoped<IInboundEmailService, DefaultInboundEmailService>();
        services.AddScoped<Application.Abstractions.Export.IExportService, BoardExportService>();
        services.AddScoped<ICalendarFeedRenderer, IcsCalendarService>();
    }
}
