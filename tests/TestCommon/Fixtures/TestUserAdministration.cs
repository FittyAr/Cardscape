using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Members;
using Microsoft.Extensions.DependencyInjection;

namespace Cardscape.Tests.Common.Fixtures;

/// <summary>
/// Test-process-only administration helpers. They mutate fixture state through
/// the real domain and persistence boundaries without exposing an HTTP bypass
/// in the product.
/// </summary>
public static class TestUserAdministration
{
    public static async Task PromoteUserToAdminAsync(
        this IServiceProvider services,
        string email,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IUserRepository users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        IClock clock = scope.ServiceProvider.GetRequiredService<IClock>();

        User user = await users.FindByEmailAsync(email, cancellationToken)
            ?? throw new InvalidOperationException($"Test user '{email}' was not found.");
        if (user.IsAdmin)
        {
            return;
        }

        user.SetAdmin(true, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
