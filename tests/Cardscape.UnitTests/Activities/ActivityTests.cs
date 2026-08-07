using Cardscape.Application.Activities.Queries;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Tests.Common.Fakes;

namespace Cardscape.UnitTests.Activities;

/// <summary>
/// Unit tests for the activity-log slice: cursor encoding,
/// limit clamping, in-memory repo paging, and DTO mapping.
/// All paths are pure (no Wolverine bus, no ICurrentUser) so we
/// can run them in milliseconds and they don't depend on the
/// web host.
/// </summary>
public class ActivityTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly BoardId TestBoard = BoardId.New();
    private static readonly CardId TestCard = CardId.New();
    private static readonly Guid TestActor = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // ── cursor encoding ─────────────────────────────────────

    [Fact]
    public void Cursor_Encode_Then_Decode_Roundtrips_OccurredAt_And_Id()
    {
        DateTimeOffset occurred = Now;
        Guid id = Guid.NewGuid();

        string cursor = ActivityCursor.Encode(occurred, id);

        ActivityCursor.TryDecode(cursor, out DateTimeOffset decodedTime, out Guid decodedId)
            .Should().BeTrue();
        decodedTime.ToUnixTimeMilliseconds().Should().Be(occurred.ToUnixTimeMilliseconds());
        decodedId.Should().Be(id);
    }

    [Fact]
    public void Cursor_TryDecode_Returns_False_For_Empty_Or_Malformed_Input()
    {
        ActivityCursor.TryDecode(null, out _, out _).Should().BeFalse();
        ActivityCursor.TryDecode(string.Empty, out _, out _).Should().BeFalse();
        ActivityCursor.TryDecode("not-base64-!!!", out _, out _).Should().BeFalse();
        ActivityCursor.TryDecode(ActivityCursor.Encode(Now, Guid.NewGuid())[..^4], out _, out _)
            .Should().BeFalse();
    }

    [Fact]
    public void Cursor_ClampLimit_Returns_Default_For_Null_Or_NonPositive_And_Caps_At_Max()
    {
        ActivityCursor.ClampLimit(null).Should().Be(ActivityCursor.DefaultLimit);
        ActivityCursor.ClampLimit(0).Should().Be(ActivityCursor.DefaultLimit);
        ActivityCursor.ClampLimit(-5).Should().Be(ActivityCursor.DefaultLimit);
        ActivityCursor.ClampLimit(25).Should().Be(25);
        ActivityCursor.ClampLimit(ActivityCursor.MaxLimit + 1).Should().Be(ActivityCursor.MaxLimit);
    }

    // ── in-memory repository: pagination semantics ──────────

    [Fact]
    public async Task InMemoryRepo_ListForBoard_Returns_Newest_First_And_Respects_Limit()
    {
        InMemoryActivityRepository repo = new();
        DateTimeOffset t0 = Now;
        for (int i = 0; i < 5; i++)
        {
            Activity a = Activity.Create(
                TestBoard, TestCard.Value, TestActor,
                ActivityKind.CardCreated, "{}", t0.AddMinutes(i));
            await repo.AddAsync(a, TestContext.Current.CancellationToken);
        }

        IReadOnlyList<Activity> page = await repo.ListForBoardAsync(
            TestBoard, limit: 3, beforeOccurredAt: null, beforeId: null, TestContext.Current.CancellationToken);

        page.Should().HaveCount(3);
        // Newest first: minutes 4, 3, 2.
        page[0].OccurredAt.Should().Be(t0.AddMinutes(4));
        page[1].OccurredAt.Should().Be(t0.AddMinutes(3));
        page[2].OccurredAt.Should().Be(t0.AddMinutes(2));
    }

    [Fact]
    public async Task InMemoryRepo_ListForBoard_Paginates_With_Cursor()
    {
        InMemoryActivityRepository repo = new();
        DateTimeOffset t0 = Now;
        List<Activity> all = [];
        for (int i = 0; i < 4; i++)
        {
            Activity a = Activity.Create(
                TestBoard, TestCard.Value, TestActor,
                ActivityKind.CardCreated, "{}", t0.AddMinutes(i));
            all.Add(a);
            await repo.AddAsync(a, TestContext.Current.CancellationToken);
        }

        IReadOnlyList<Activity> first = await repo.ListForBoardAsync(
            TestBoard, limit: 2, beforeOccurredAt: null, beforeId: null, TestContext.Current.CancellationToken);
        first.Should().HaveCount(2);
        Activity cursor = first[^1];

        IReadOnlyList<Activity> second = await repo.ListForBoardAsync(
            TestBoard, limit: 10, beforeOccurredAt: cursor.OccurredAt, beforeId: cursor.Id.Value, TestContext.Current.CancellationToken);
        second.Should().HaveCount(2);
        second[0].OccurredAt.Should().Be(t0.AddMinutes(1));
        second[1].OccurredAt.Should().Be(t0.AddMinutes(0));
    }

    [Fact]
    public async Task InMemoryRepo_ListForCard_Filters_To_Requested_Card_Only()
    {
        InMemoryActivityRepository repo = new();
        CardId otherCard = CardId.New();
        await repo.AddAsync(Activity.Create(TestBoard, TestCard.Value, TestActor, ActivityKind.CardCreated, "{}", Now), TestContext.Current.CancellationToken);
        await repo.AddAsync(Activity.Create(TestBoard, otherCard.Value, TestActor, ActivityKind.CardCreated, "{}", Now), TestContext.Current.CancellationToken);
        await repo.AddAsync(Activity.Create(TestBoard, TestCard.Value, TestActor, ActivityKind.CardRenamed, "{}", Now), TestContext.Current.CancellationToken);

        IReadOnlyList<Activity> rows = await repo.ListForCardAsync(
            TestCard, limit: 10, beforeOccurredAt: null, beforeId: null, TestContext.Current.CancellationToken);

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(a => a.CardId == TestCard.Value);
    }

    // ── DTO mapping ─────────────────────────────────────────

    [Fact]
    public void Dto_FromEntity_Projects_Every_Visible_Field()
    {
        DateTimeOffset at = Now;
        Activity source = Activity.Create(
            TestBoard, TestCard.Value, TestActor, ActivityKind.CardMoved, "{\"from\":\"a\"}", at);

        ActivityDto dto = ActivityDto.FromEntity(source, new Dictionary<Guid, string>());

        dto.Id.Should().Be(source.Id.Value);
        dto.BoardId.Should().Be(source.BoardId.Value);
        dto.CardId.Should().Be(source.CardId);
        dto.ActorId.Should().Be(source.ActorId);
        dto.Kind.Should().Be((int)ActivityKind.CardMoved);
        dto.KindName.Should().Be(nameof(ActivityKind.CardMoved));
        dto.PayloadJson.Should().Be("{\"from\":\"a\"}");
        dto.OccurredAt.Should().Be(at);
    }
}
