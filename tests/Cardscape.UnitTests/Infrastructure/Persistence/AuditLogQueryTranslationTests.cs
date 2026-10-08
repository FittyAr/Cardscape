using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.UnitTests.Infrastructure.Persistence;

/// <summary>
/// The integration suite runs on SQLite; this translates the audit-log
/// listing with every filter set on PostgreSQL (no connection is opened),
/// so an untranslatable shape fails here instead of as a production 500.
/// </summary>
public sealed class AuditLogQueryTranslationTests
{
    [Fact]
    public void FilteredPage_TranslatesOnPostgreSql()
    {
        DbContextOptions<CardscapeDbContext> options = new DbContextOptionsBuilder<CardscapeDbContext>()
            .UseNpgsql("Host=translation-only;Database=unused")
            .Options;
        using var db = new CardscapeDbContext(options);
        AuditLogFilter filter = new(
            From: new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            To: new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
            ActionPrefix: "workspace.",
            ActorUserId: Guid.NewGuid(),
            TargetUserId: Guid.NewGuid(),
            WorkspaceId: Guid.NewGuid(),
            Search: "Ada");

        string sql = AuditLogReader.Page(AuditLogReader.Query(db, filter), 25, 25).ToQueryString();

        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("lower(");
        sql.Should().MatchRegex("(?i)\"OccurredAtUtcTicks\" >= @", "the date range must run in SQL");
    }
}
