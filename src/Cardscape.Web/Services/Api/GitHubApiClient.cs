using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IGitHubApiClient
{
    Task<ApiResult> LinkRepoAsync(
        Guid boardId, string repoFullName, IReadOnlyList<string> events, CancellationToken ct = default);
    Task<ApiResult<IReadOnlyList<GitHubPullRequestDto>>> ListPullsAsync(
        Guid boardId, string repoFullName, string? state, CancellationToken ct = default);
    Task<ApiResult<GitHubPullRequestLinkDto>> LinkPullAsync(
        Guid cardId, string repoFullName, int pullRequestNumber, CancellationToken ct = default);
    Task<ApiResult<GitHubIssueDto>> CreateIssueAsync(
        Guid cardId, string repoFullName, string? title, string? body, CancellationToken ct = default);
}

public sealed class GitHubApiClient(IHttpClientFactory http) : ApiClientBase(http), IGitHubApiClient
{
    public async Task<ApiResult> LinkRepoAsync(
        Guid boardId, string repoFullName, IReadOnlyList<string> events, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/integrations/github/connect",
            new LinkGitHubRepoRequest(boardId, repoFullName, events), ct);
        return await ReadAsync(response, ct);
    }

    public async Task<ApiResult<IReadOnlyList<GitHubPullRequestDto>>> ListPullsAsync(
        Guid boardId, string repoFullName, string? state, CancellationToken ct = default)
    {
        string stateParam = string.IsNullOrWhiteSpace(state) ? "open" : state;
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/integrations/github/pulls?boardId={boardId:D}&repoFullName={Uri.EscapeDataString(repoFullName)}&state={stateParam}", ct);
        return await ReadAsync<IReadOnlyList<GitHubPullRequestDto>>(response, ct);
    }

    public async Task<ApiResult<GitHubPullRequestLinkDto>> LinkPullAsync(
        Guid cardId, string repoFullName, int pullRequestNumber, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/integrations/github/pulls/link",
            new LinkGitHubPullRequestRequest(cardId, repoFullName, pullRequestNumber), ct);
        return await ReadAsync<GitHubPullRequestLinkDto>(response, ct);
    }

    public async Task<ApiResult<GitHubIssueDto>> CreateIssueAsync(
        Guid cardId, string repoFullName, string? title, string? body, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/integrations/github/issues",
            new CreateGitHubIssueRequest(cardId, repoFullName, title, body), ct);
        return await ReadAsync<GitHubIssueDto>(response, ct);
    }

    public sealed record LinkGitHubRepoRequest(
        Guid BoardId, string RepoFullName, IReadOnlyList<string> Events);

    public sealed record LinkGitHubPullRequestRequest(
        Guid CardId, string RepoFullName, int PullRequestNumber);

    public sealed record CreateGitHubIssueRequest(
        Guid CardId, string RepoFullName, string? Title, string? Body);
}
