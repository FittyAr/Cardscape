using Cardscape.Domain.Boards;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.UnitTests.Infrastructure.Persistence;

/// <summary>
/// The integration suite runs on SQLite, which takes a client-side path
/// for <see cref="DateTimeOffset"/> filters. These tests translate the
/// server-side calendar query with the PostgreSQL provider (no connection
/// is opened) so an untranslatable LINQ shape fails here instead of as a
/// 500 on production databases.
/// </summary>
public sealed class CalendarQueryTranslationTests
{
    private static readonly DateTimeOffset From = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QueryCalendarRange_TranslatesOnPostgreSql(bool scopedToBoard)
    {
        DbContextOptions<CardscapeDbContext> options = new DbContextOptionsBuilder<CardscapeDbContext>()
            .UseNpgsql("Host=translation-only;Database=unused")
            .Options;
        using var db = new CardscapeDbContext(options);
        BoardId? boardId = scopedToBoard ? BoardId.New() : null;

        string sql = CardRepository
            .QueryCalendarRange(db, Guid.NewGuid(), boardId, From, From.AddMonths(1))
            .ToQueryString();

        sql.Should().Contain("ORDER BY");
        sql.Should().MatchRegex("(?i)due_?date\"? >= @", "the range filter must run in SQL, not after projection");
    }
}
