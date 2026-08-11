using Cardscape.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cardscape.Api.BackgroundJobs;

public static class BackgroundJobRegistrationExtensions
{
    /// <summary>
    /// Registers the dispatcher <see cref="IHostedService"/> and the
    /// options that tune it. Call from <c>Program.cs</c> after the
    /// infrastructure registration.
    /// </summary>
    public static IServiceCollection AddCardscapeBackgroundJobDispatcher(
        this IServiceCollection services,
        Action<BackgroundJobDispatcherOptions>? configure = null)
    {
        BackgroundJobDispatcherOptions options = new();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddHostedService<BackgroundJobDispatcherService>();
        services.AddHostedService<CardRecurrenceDispatcherService>();
        services.AddHostedService<RateLimitBucketEvictionService>();
        return services;
    }
}
