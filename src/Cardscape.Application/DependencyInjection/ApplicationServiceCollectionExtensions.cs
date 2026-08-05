using System.Reflection;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Security;
using FluentValidation;
using JasperFx.CodeGeneration.Model;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Cardscape.Application.DependencyInjection;

/// <summary>DI extensions for the Application layer.</summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers Wolverine (which discovers the static command/query
    /// handlers in this assembly), all FluentValidation validators,
    /// and the shared abstractions (<see cref="IClock"/>,
    /// <see cref="ICurrentUser"/>, <see cref="IPasswordHasher"/>,
    /// <see cref="ITokenService"/>).
    /// <para>
    /// Optional <paramref name="additionalAssemblies"/> lets the
    /// caller include static handlers in sibling projects
    /// (the API hosts its own Wolverine handlers for the
    /// SignalR / MCP broadcaster; without including the API
    /// assembly, those handlers are silently skipped and
    /// the broadcaster never fires).
    /// </para>
    /// </summary>
    public static IServiceCollection AddCardscapeApplication(
        this IServiceCollection services,
        params Assembly[] additionalAssemblies)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddWolverine(opts =>
        {
            opts.Discovery.IncludeAssembly(assembly);
            foreach (Assembly extra in additionalAssemblies)
            {
                opts.Discovery.IncludeAssembly(extra);
            }
            // EF Core registers DbContextOptions as a scoped factory, which
            // conflicts with Wolverine's default "no service location" policy.
            opts.ServiceLocationPolicy = ServiceLocationPolicy.AllowedButWarn;
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentUser, CurrentUser>();

        return services;
    }

    private sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
