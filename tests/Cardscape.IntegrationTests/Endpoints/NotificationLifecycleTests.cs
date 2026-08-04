using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.IntegrationTests.Fixtures;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// End-to-end coverage of the notification inbox: a user is
/// assigned to a card, a notification is created, listed via the
/// inbox, marked read, and the unread count drops to zero.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class NotificationLifecycleTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public NotificationLifecycleTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task AssignCard_Creates_Notification_For_Assignee()
    {
        // Owner + assignee are two different users.
        (HttpClient owner, UserSummary _) = await CreateAuthenticatedClientAsync("Owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner, "Inbox WS");
        BoardDto board = await CreateBoardAsync(owner, ws.Id, "Inbox Board");
        BoardListDto list = await CreateListAsync(owner, board.Id, "Todo");
        CardDto card = await CreateCardAsync(owner, list.Id, "Need eyes on this");

        (HttpClient assignee, UserSummary assigneeUser) = await CreateAuthenticatedClientAsync("Assignee",
            emailOverride: $"assignee-{Guid.NewGuid():N}@cardscape.local");

        HttpResponseMessage assign = await owner.PostAsync(
            $"api/cards/{card.Id}/assign/{assigneeUser.Id}", content: null, TestContext.Current.CancellationToken);
        assign.IsSuccessStatusCode.Should().BeTrue();

        // The assignee's inbox shows one unread notification.
        HttpResponseMessage inbox = await assignee.GetAsync("api/notifications/?unreadOnly=true&skip=0&take=10", TestContext.Current.CancellationToken);
        inbox.IsSuccessStatusCode.Should().BeTrue();
        NotificationDto[]? rows = await inbox.Content.ReadFromJsonAsync<NotificationDto[]>(TestContext.Current.CancellationToken);
        rows.Should().NotBeNull().And.HaveCount(1);
        rows![0].Kind.Should().Be("AssignedToCard");
        rows[0].IsRead.Should().BeFalse();
        rows[0].PayloadJson.Should().Contain(card.Id.ToString());

        // The unread count is 1.
        UnreadCountDto count = (await (await assignee.GetAsync("api/notifications/unread-count", TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<UnreadCountDto>(TestContext.Current.CancellationToken))!;
        count.Count.Should().Be(1);

        // Mark the notification read.
        HttpResponseMessage mark = await assignee.PostAsync(
            $"api/notifications/{rows[0].Id}/read", content: null, TestContext.Current.CancellationToken);
        mark.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Unread count is now 0.
        UnreadCountDto after =
            (await (await assignee.GetAsync("api/notifications/unread-count", TestContext.Current.CancellationToken))
                .Content.ReadFromJsonAsync<UnreadCountDto>(TestContext.Current.CancellationToken))!;
        after.Count.Should().Be(0);
    }

    [Fact]
    public async Task Self_Assign_Does_Not_Create_Notification()
    {
        (HttpClient owner, UserSummary me) = await CreateAuthenticatedClientAsync("Owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner, "SelfAssign WS");
        BoardDto board = await CreateBoardAsync(owner, ws.Id, "SelfAssign Board");
        BoardListDto list = await CreateListAsync(owner, board.Id, "Todo");
        CardDto card = await CreateCardAsync(owner, list.Id, "Solo work");

        HttpResponseMessage assign = await owner.PostAsync(
            $"api/cards/{card.Id}/assign/{me.Id}", content: null, TestContext.Current.CancellationToken);
        assign.IsSuccessStatusCode.Should().BeTrue();

        HttpResponseMessage inbox = await owner.GetAsync("api/notifications/?unreadOnly=true&skip=0&take=10", TestContext.Current.CancellationToken);
        inbox.IsSuccessStatusCode.Should().BeTrue();
        IReadOnlyList<NotificationDto> rows = (await inbox.Content
            .ReadFromJsonAsync<IReadOnlyList<NotificationDto>>(TestContext.Current.CancellationToken))!;
        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task Mark_All_Read_Clears_Everything()
    {
        (HttpClient owner, UserSummary _) = await CreateAuthenticatedClientAsync("Owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner, "MarkAll WS");
        BoardDto board = await CreateBoardAsync(owner, ws.Id, "MarkAll Board");
        BoardListDto list = await CreateListAsync(owner, board.Id, "Todo");

        (HttpClient other, UserSummary otherUser) = await CreateAuthenticatedClientAsync("Other",
            emailOverride: $"other-{Guid.NewGuid():N}@cardscape.local");

        for (int i = 0; i < 3; i++)
        {
            CardDto c = await CreateCardAsync(owner, list.Id, $"Card {i}");
            await owner.PostAsync($"api/cards/{c.Id}/assign/{otherUser.Id}", content: null, TestContext.Current.CancellationToken);
        }

        UnreadCountDto before =
            (await (await other.GetAsync("api/notifications/unread-count", TestContext.Current.CancellationToken))
                .Content.ReadFromJsonAsync<UnreadCountDto>(TestContext.Current.CancellationToken))!;
        before.Count.Should().Be(3);

        HttpResponseMessage markAll = await other.PostAsync(
            "api/notifications/mark-all-read", content: null, TestContext.Current.CancellationToken);
        markAll.StatusCode.Should().Be(HttpStatusCode.NoContent);

        UnreadCountDto after =
            (await (await other.GetAsync("api/notifications/unread-count", TestContext.Current.CancellationToken))
                .Content.ReadFromJsonAsync<UnreadCountDto>(TestContext.Current.CancellationToken))!;
        after.Count.Should().Be(0);
    }

    [Fact]
    public async Task Anonymous_Inbox_Returns_Unauthorized()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage resp = await client.GetAsync("api/notifications/", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── helpers ────────────────────────────────────────────────

    private async Task<(HttpClient client, UserSummary user)> CreateAuthenticatedClientAsync(
        string displayNamePrefix, string? emailOverride = null)
    {
        HttpClient client = _factory.CreateApiClient();
        string email = emailOverride ?? $"{displayNamePrefix}-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, $"{displayNamePrefix} User", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth.User);
    }

    private static async Task<WorkspaceDto> CreateWorkspaceAsync(HttpClient client, string name)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync("api/workspaces/", new { name });
        resp.IsSuccessStatusCode.Should().BeTrue();
        return (await resp.Content.ReadFromJsonAsync<WorkspaceDto>())!;
    }

    private static async Task<BoardDto> CreateBoardAsync(HttpClient client, Guid workspaceId, string name)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/boards/", new { workspaceId, name, description = (string?)null, visibility = 0 });
        resp.IsSuccessStatusCode.Should().BeTrue();
        return (await resp.Content.ReadFromJsonAsync<BoardDto>())!;
    }

    private static async Task<BoardListDto> CreateListAsync(HttpClient client, Guid boardId, string name)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/lists/", new { boardId, name });
        resp.IsSuccessStatusCode.Should().BeTrue();
        return (await resp.Content.ReadFromJsonAsync<BoardListDto>())!;
    }

    private static async Task<CardDto> CreateCardAsync(HttpClient client, Guid listId, string title)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/cards/", new { listId, title, description = (string?)null });
        resp.IsSuccessStatusCode.Should().BeTrue();
        return (await resp.Content.ReadFromJsonAsync<CardDto>())!;
    }

    // ── DTOs (mirror the API) ──────────────────────────────────

    public sealed record WorkspaceDto(Guid Id, Guid OwnerId, string Name, int MemberCount);
    public sealed record BoardDto(Guid Id, Guid WorkspaceId, string Name, int Visibility, bool IsArchived, bool IsStarred, DateTimeOffset CreatedAt);
    public sealed record BoardListDto(Guid Id, Guid BoardId, string Name, double Position, bool IsArchived, DateTimeOffset CreatedAt, int CardCount);
    public sealed record CardDto(Guid Id, Guid ListId, string Title, string Description, double Position, DateTimeOffset? DueDate, bool IsArchived, bool IsCompleted, string? CoverColor, DateTimeOffset CreatedAt, int MemberCount, int LabelCount);
    public sealed record NotificationDto(Guid Id, Guid UserId, string Kind, string PayloadJson, bool IsRead, DateTimeOffset? ReadAt, DateTimeOffset CreatedAt);
    public sealed record UnreadCountDto(int Count);
}
