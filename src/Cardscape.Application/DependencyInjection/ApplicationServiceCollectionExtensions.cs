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
    /// </summary>
    public static IServiceCollection AddCardscapeApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddWolverine(opts =>
        {
            opts.Discovery.IncludeAssembly(assembly);
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
