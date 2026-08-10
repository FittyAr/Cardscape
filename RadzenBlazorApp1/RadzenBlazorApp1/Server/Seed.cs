using Microsoft.EntityFrameworkCore;
using RadzenBlazorApp1.Server.Data;
public static class Seed
{
    public static async Task<IHost> SeedDataAsync(this IHost host)
    {
        using (var scope = host.Services.CreateScope())
        {
            var identityContext = scope.ServiceProvider.GetRequiredService<ApplicationIdentityDbContext>();
            await identityContext.Database.MigrateAsync();

            if (!await identityContext.Users.AnyAsync())
            {
                await identityContext.Seed();
            }

            var crmContext = scope.ServiceProvider.GetRequiredService<CrmDBContext>();
            await crmContext.Database.MigrateAsync();
            if (!await crmContext.Contacts.AnyAsync())
            {
                await crmContext.Seed();
            }
        }
        return host;
    }
}