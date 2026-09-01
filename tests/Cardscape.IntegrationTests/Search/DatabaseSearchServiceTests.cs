using System.Globalization;
using Cardscape.Application.Abstractions.Search;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Comments;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.IntegrationTests.Search;

public sealed class DatabaseSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_PersistedEntityKinds_ReturnsAccentInsensitiveResults()
    {
        string databasePath = NewDatabasePath();
        try
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            DbContextOptions<CardscapeDbContext> options = Options(databasePath);
            Guid boardId = Guid.NewGuid();
            Guid actorId = Guid.NewGuid();
            SeedCard seed = CardFor(boardId, "Rehidratáble card", archived: false);

            await using (var seedContext = new CardscapeDbContext(options))
            {
                await seedContext.Database.EnsureCreatedAsync(ct);
                seedContext.Lists.Add(BoardList.Create(
                    seed.Card.ListId, new BoardId(boardId), ListName.Create("Search list").Value,
                    Position.Start(), actorId, DateTimeOffset.Parse("2026-08-21T12:00:00Z", CultureInfo.InvariantCulture)).Value);
                seedContext.Cards.Add(seed.Card);
                seedContext.Comments.Add(Comment.Create(
                    CommentId.New(), seed.Card.Id, actorId,
                    CommentBody.Create("Rehidratáble comment").Value,
                    DateTimeOffset.Parse("2026-08-21T12:01:00Z", CultureInfo.InvariantCulture)).Value);
                Checklist checklist = Checklist.Create(
                    ChecklistId.New(), seed.Card.Id,
                    ChecklistTitle.Create("Rehidratáble checklist").Value,
                    actorId, DateTimeOffset.Parse("2026-08-21T12:02:00Z", CultureInfo.InvariantCulture)).Value;
                checklist.AddItem(
                    ChecklistItemText.Create("Rehidratáble item").Value,
                    Position.Start(), DateTimeOffset.Parse("2026-08-21T12:03:00Z", CultureInfo.InvariantCulture));
                seedContext.Checklists.Add(checklist);
                seedContext.Labels.Add(Label.Create(
                    LabelId.New(), new BoardId(boardId),
                    LabelName.Create("Rehidratáble label").Value,
                    Color.Create("#123456").Value, actorId,
                    DateTimeOffset.Parse("2026-08-21T12:04:00Z", CultureInfo.InvariantCulture)).Value);
                seedContext.Activities.Add(Activity.Create(
                    new BoardId(boardId), seed.Card.Id.Value, actorId,
                    ActivityKind.CardCreated, "{\"text\":\"Rehidratáble activity\"}",
                    DateTimeOffset.Parse("2026-08-21T12:05:00Z", CultureInfo.InvariantCulture)));
                await seedContext.SaveChangesAsync(ct);
            }

            await using (var freshContext = new CardscapeDbContext(options))
            {
                var service = new DatabaseSearchService(freshContext);
                SearchPage page = await service.SearchAsync(
                    "rehidratable", null, null, 1, 20,
                    new HashSet<Guid> { boardId }, ct);

                page.Total.Should().Be(6);
                page.Hits.Should().HaveCount(6);
                page.Hits.Select(hit => hit.Kind).Should().BeEquivalentTo(
                    [
                        SearchHitKind.Card,
                        SearchHitKind.Comment,
                        SearchHitKind.ChecklistItem,
                        SearchHitKind.ChecklistItem,
                        SearchHitKind.Label,
                        SearchHitKind.Activity
                    ]);
                page.Hits.Should().OnlyContain(hit => hit.BoardId == boardId && hit.Score == 1);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task SearchAsync_PersistedCardsInFreshContext_RespectsAuthorizationAndArchivedState()
    {
        string databasePath = NewDatabasePath();
        try
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            DbContextOptions<CardscapeDbContext> options = Options(databasePath);
            Guid readableBoardId = Guid.NewGuid();
            Guid forbiddenBoardId = Guid.NewGuid();
            SeedCard readable = CardFor(readableBoardId, "Persistent roadmap", archived: false);
            SeedCard forbidden = CardFor(forbiddenBoardId, "Persistent secret", archived: false);
            SeedCard archived = CardFor(readableBoardId, "Persistent archived", archived: true);
            await SeedAsync(options, ct, readable, forbidden, archived);

            await using (var freshContext = new CardscapeDbContext(options))
            {
                var service = new DatabaseSearchService(freshContext);
                SearchPage page = await service.SearchAsync(
                    "persistent", null, SearchHitKind.Card, 1, 20,
                    new HashSet<Guid> { readableBoardId }, ct);

                page.Total.Should().Be(1);
                page.Hits.Should().ContainSingle()
                    .Which.Id.Should().Be(readable.Card.Id.Value.ToString());
                page.Hits[0].BoardId.Should().Be(readableBoardId);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task SearchAsync_DeletedCardInFreshContext_DoesNotReturnStaleHit()
    {
        string databasePath = NewDatabasePath();
        try
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            DbContextOptions<CardscapeDbContext> options = Options(databasePath);
            Guid boardId = Guid.NewGuid();
            SeedCard card = CardFor(boardId, "Ephemeral deletion target", archived: false);
            await SeedAsync(options, ct, card);

            await using (var deleteContext = new CardscapeDbContext(options))
            {
                Card persisted = await deleteContext.Cards.SingleAsync(item => item.Id == card.Card.Id, ct);
                deleteContext.Cards.Remove(persisted);
                await deleteContext.SaveChangesAsync(ct);
            }

            await using (var freshContext = new CardscapeDbContext(options))
            {
                var service = new DatabaseSearchService(freshContext);
                SearchPage page = await service.SearchAsync(
                    "ephemeral", null, SearchHitKind.Card, 1, 20,
                    new HashSet<Guid> { boardId }, ct);

                page.Total.Should().Be(0);
                page.Hits.Should().BeEmpty();
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    private static SeedCard CardFor(Guid boardId, string title, bool archived)
    {
        var listId = new BoardListId(Guid.NewGuid());
        Card card = Card.Create(
            CardId.New(), listId, CardTitle.Create(title).Value,
            CardDescription.Create("Searchable description").Value,
            Position.Start(), Guid.NewGuid(),
            DateTimeOffset.Parse("2026-08-21T12:00:00Z", CultureInfo.InvariantCulture)).Value;
        if (archived)
        {
            card.Archive(DateTimeOffset.Parse("2026-08-21T12:01:00Z", CultureInfo.InvariantCulture));
        }

        return new SeedCard(boardId, card);
    }

    private static async Task SeedAsync(
        DbContextOptions<CardscapeDbContext> options,
        CancellationToken ct,
        params SeedCard[] cards)
    {
        await using var context = new CardscapeDbContext(options);
        await context.Database.EnsureCreatedAsync(ct);
        foreach (SeedCard seed in cards)
        {
            context.Lists.Add(BoardList.Create(
                seed.Card.ListId, new BoardId(seed.BoardId),
                ListName.Create("Search list").Value, Position.Start(),
                Guid.NewGuid(),
                DateTimeOffset.Parse("2026-08-21T12:00:00Z", CultureInfo.InvariantCulture)).Value);
        }

        context.Cards.AddRange(cards.Select(seed => seed.Card));
        await context.SaveChangesAsync(ct);
    }

    private static DbContextOptions<CardscapeDbContext> Options(string path) =>
        new DbContextOptionsBuilder<CardscapeDbContext>()
            .UseSqlite($"Data Source={path};Pooling=False")
            .Options;

    private static string NewDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"cardscape-search-{Guid.NewGuid():N}.db");

    private static void DeleteDatabase(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed record SeedCard(Guid BoardId, Card Card);
}
