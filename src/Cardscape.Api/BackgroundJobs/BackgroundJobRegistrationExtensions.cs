using Cardscape.Application.Abstractions;
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

    /// <summary>
    /// Scans the DI container for every <see cref="IBackgroundJobHandler"/>
    /// implementation and registers it with the
    /// <see cref="IBackgroundJobHandlerRegistry"/>. Call this once at
    /// startup before the dispatcher service starts polling.
    /// </summary>
    public static IServiceProvider UseCardscapeBackgroundJobHandlers(this IServiceProvider sp)
    {
        IBackgroundJobHandlerRegistry registry =
            sp.GetRequiredService<IBackgroundJobHandlerRegistry>();
        foreach (IBackgroundJobHandler handler in sp.GetServices<IBackgroundJobHandler>())
        {
            registry.Register(handler);
        }
        return sp;
    }
}
