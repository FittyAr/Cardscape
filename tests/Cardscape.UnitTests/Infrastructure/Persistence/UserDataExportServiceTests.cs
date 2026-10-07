using Cardscape.Application.Users.Queries;
using Cardscape.Domain.Integrations.OAuthApps;
using Cardscape.Domain.Members;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Tests.Common.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.UnitTests.Infrastructure.Persistence;

public sealed class UserDataExportServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BuildExport_OAuthApp_ExportsTheDisplayPrefixNotTheSecretHash()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<CardscapeDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new CardscapeDbContext(options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        User user = User.Register(
            UserId.New(),
            EmailAddress.Create("export@cardscape.local").Value,
            DisplayName.Create("Export Test").Value,
            PasswordHash.FromHashed("v1.salt.hash").Value,
            Now).Value;
        OAuthApp app = OAuthApp.Register(
            OAuthAppId.New(),
            "Exporter",
            "client-id",
            clientSecretHash: "HASHEDSECRETVALUE0123456789",
            clientSecretPrefix: "csk_abcd",
            user.Id.Value,
            ["read"],
            ["https://example.test/callback"],
            Now).Value;
        db.Users.Add(user);
        db.OAuthApps.Add(app);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        UserDataExportDto? export = await new UserDataExportService(db, new FakeClock(Now))
            .BuildExportAsync(user.Id, TestContext.Current.CancellationToken);

        export!.OAuthApps.Should().ContainSingle()
            .Which.SecretPrefix.Should().Be("csk_abcd");
    }
}
