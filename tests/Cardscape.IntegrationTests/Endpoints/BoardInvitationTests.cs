using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Workspaces;
using Microsoft.AspNetCore.Mvc.Testing;
using static Cardscape.IntegrationTests.Endpoints.WorkspaceMemberAdministrationTests;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// "Invite to this board": one invitation joins the workspace (as a guest)
/// and the board, in one step.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class BoardInvitationTests(CardscapeWebApplicationFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GuestInvitedToABoard_JoinsWorkspaceAndBoard_OnSignUp()
    {
        using WebApplicationFactory<Program> host = factory.WithWebHostBuilder(_ => { });
        Account owner = await RegisterAsync(host, "owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, "Board invites");
        Guid board = await CreateBoardAsync(owner.Client, ws.Id);

        string email = NewEmail("guest");
        string token = await IssueAsync(owner.Client, ws.Id, email, "guest", board, "observer");

        // Pending list and preview name the board.
        using (JsonDocument preview = JsonDocument.Parse(await (await host.CreateClient()
            .PostAsJsonAsync("api/invitations/preview", new { token }, Ct)).Content.ReadAsStringAsync(Ct)))
        {
            preview.RootElement.GetProperty("boardName").GetString().Should().Be("Invite board");
        }

        HttpResponseMessage register = await host.CreateClient().PostAsJsonAsync(
            "api/auth/register", new RegisterRequest(email, "Guest", Password, token), Ct);
        register.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid guestId = (await register.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options, Ct))!.User.Id;

        (await MembersAsync(owner.Client, ws.Id)).Should().Contain(m => m.UserId == guestId && m.Role == WorkspaceRole.Guest);
        (await BoardMembersAsync(owner.Client, board)).Should().Contain(m => m.UserId == guestId && m.Role == BoardMemberRole.Observer);
    }

    [Fact]
    public async Task ExistingMember_AcceptingABoardInvitation_IsAddedToTheBoard()
    {
        using WebApplicationFactory<Program> host = factory.WithWebHostBuilder(_ => { });
        Account owner = await RegisterAsync(host, "owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, "Board invites");
        Guid board = await CreateBoardAsync(owner.Client, ws.Id);
        Account member = await RegisterAsync(host, "member");
        string first = await InviteAsync(owner.Client, ws.Id, member.Email, "member");
        (await member.Client.PostAsJsonAsync("api/invitations/accept", new { token = first }, Ct)).EnsureSuccessStatusCode();

        string token = await IssueAsync(owner.Client, ws.Id, member.Email, "member", board, "member");
        (await member.Client.PostAsJsonAsync("api/invitations/accept", new { token }, Ct)).EnsureSuccessStatusCode();

        (await BoardMembersAsync(owner.Client, board)).Should().Contain(m => m.UserId == member.Id && m.Role == BoardMemberRole.Member);
    }

    [Fact]
    public async Task BoardAdmins_InviteOnlyGuests_AndGuestsCannotBeBoardAdmins()
    {
        using WebApplicationFactory<Program> host = factory.WithWebHostBuilder(_ => { });
        Account owner = await RegisterAsync(host, "owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, "Board invites");
        Guid board = await CreateBoardAsync(owner.Client, ws.Id);
        Account boardAdmin = await RegisterAsync(host, "boardadmin");
        string join = await InviteAsync(owner.Client, ws.Id, boardAdmin.Email, "member");
        (await boardAdmin.Client.PostAsJsonAsync("api/invitations/accept", new { token = join }, Ct)).EnsureSuccessStatusCode();
        (await owner.Client.PostAsJsonAsync($"api/boards/{board}/members", new { userId = boardAdmin.Id, role = "admin" }, Ct))
            .EnsureSuccessStatusCode();

        (await PostInvitationAsync(boardAdmin.Client, ws.Id, NewEmail("g"), "guest", board, "member"))
            .StatusCode.Should().Be(HttpStatusCode.Created, "a board Admin may bring guests to their board");
        (await PostInvitationAsync(boardAdmin.Client, ws.Id, NewEmail("m"), "member", board, "member"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "only workspace managers invite full members");
        (await PostInvitationAsync(boardAdmin.Client, ws.Id, NewEmail("w"), "guest", null, null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "without a board it is a workspace invitation");
        (await PostInvitationAsync(owner.Client, ws.Id, NewEmail("a"), "guest", board, "admin"))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await PostInvitationAsync(owner.Client, ws.Id, NewEmail("x"), "guest", Guid.NewGuid(), "member"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static Task<HttpResponseMessage> PostInvitationAsync(
        HttpClient client, Guid workspaceId, string email, string role, Guid? boardId, string? boardRole) =>
        client.PostAsJsonAsync($"api/workspaces/{workspaceId}/invitations/", new { email, role, boardId, boardRole }, Ct);

    private static async Task<string> IssueAsync(
        HttpClient client, Guid workspaceId, string email, string role, Guid boardId, string boardRole)
    {
        HttpResponseMessage response = await PostInvitationAsync(client, workspaceId, email, role, boardId, boardRole);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return doc.RootElement.GetProperty("cleartextToken").GetString()!;
    }

    private static async Task<Guid> CreateBoardAsync(HttpClient client, Guid workspaceId)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/boards/", new { workspaceId, name = "Invite board", description = (string?)null, visibility = "private" }, Ct);
        response.IsSuccessStatusCode.Should().BeTrue();
        return (await response.Content.ReadFromJsonAsync<BoardDto>(TestJson.Options, Ct))!.Id;
    }

    private static async Task<IReadOnlyList<BoardMemberDto>> BoardMembersAsync(HttpClient client, Guid boardId) =>
        (await client.GetFromJsonAsync<BoardMemberDto[]>($"api/boards/{boardId}/members", TestJson.Options, Ct))!;
}
