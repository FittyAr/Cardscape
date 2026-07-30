using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Application.Integrations.GitHub.DTOs;
using Cardscape.Domain.Common;
using Microsoft.Extensions.Configuration;

namespace Cardscape.Infrastructure.Integrations;

/// <summary>
/// Default <see cref="IGitHubService"/> that talks to the GitHub
/// REST API. The base URL is
/// <c>https://api.github.com</c>; authentication is a personal
/// access token (classic or fine-grained) read from
/// <c>Integrations:GitHub:Token</c>. The implementation degrades
/// gracefully when the token is absent: every call returns a
/// domain <see cref="ErrorType.External"/> error so the caller
/// can surface a "not connected" message instead of crashing.
/// </summary>
public sealed class HttpGitHubService : IGitHubService
{
    private const string GitHubApiBase = "https://api.github.com";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string? _token;

    public HttpGitHubService(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _token = configuration["Integrations:GitHub:Token"];

        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Cardscape", "1.0"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        if (!string.IsNullOrWhiteSpace(_token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }
    }

    public async Task<Result<IReadOnlyList<GitHubBranchDto>>> ListBranchesAsync(
        string repoFullName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            return Result.Failure<IReadOnlyList<GitHubBranchDto>>(DomainError.External(
                "github.token_missing",
                "GitHub token is not configured (Integrations:GitHub:Token)."));
        }

        try
        {
            GitHubBranchWire[]? rows = await _http.GetFromJsonAsync<GitHubBranchWire[]>(
                $"{GitHubApiBase}/repos/{Uri.EscapeDataString(repoFullName)}/branches",
                JsonOptions,
                ct);
            if (rows is null)
            {
                return Result.Success<IReadOnlyList<GitHubBranchDto>>([]);
            }

            return Result.Success<IReadOnlyList<GitHubBranchDto>>(
                rows.Select(b => new GitHubBranchDto(
                    Name: b.Name,
                    LastCommitSha: b.Commit?.Sha,
                    Protected: b.Protected))
                .ToList());
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<IReadOnlyList<GitHubBranchDto>>(DomainError.External(
                "github.transport_error", $"GitHub API call failed: {ex.Message}"));
        }
        catch (JsonException ex)
        {
            return Result.Failure<IReadOnlyList<GitHubBranchDto>>(DomainError.External(
                "github.response_invalid", $"GitHub returned an unparseable response: {ex.Message}"));
        }
    }

    public async Task<Result<IReadOnlyList<GitHubPullRequestDto>>> ListPullRequestsAsync(
        string repoFullName, string state, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            return Result.Failure<IReadOnlyList<GitHubPullRequestDto>>(DomainError.External(
                "github.token_missing",
                "GitHub token is not configured (Integrations:GitHub:Token)."));
        }

        string url = $"{GitHubApiBase}/repos/{Uri.EscapeDataString(repoFullName)}/pulls";
        if (!string.IsNullOrWhiteSpace(state))
        {
            url += $"?state={Uri.EscapeDataString(state)}";
        }

        try
        {
            GitHubPullRequestWire[]? rows = await _http.GetFromJsonAsync<GitHubPullRequestWire[]>(
                url, JsonOptions, ct);
            if (rows is null)
            {
                return Result.Success<IReadOnlyList<GitHubPullRequestDto>>([]);
            }

            return Result.Success<IReadOnlyList<GitHubPullRequestDto>>(
                rows.Select(p => new GitHubPullRequestDto(
                    Number: p.Number,
                    Title: p.Title,
                    State: p.State,
                    Url: p.HtmlUrl,
                    HeadRef: p.Head?.Ref,
                    BaseRef: p.Base?.Ref,
                    CreatedAt: p.CreatedAt))
                .ToList());
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<IReadOnlyList<GitHubPullRequestDto>>(DomainError.External(
                "github.transport_error", $"GitHub API call failed: {ex.Message}"));
        }
        catch (JsonException ex)
        {
            return Result.Failure<IReadOnlyList<GitHubPullRequestDto>>(DomainError.External(
                "github.response_invalid", $"GitHub returned an unparseable response: {ex.Message}"));
        }
    }

    public async Task<Result<IReadOnlyList<GitHubIssueDto>>> ListIssuesAsync(
        string repoFullName, string state, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            return Result.Failure<IReadOnlyList<GitHubIssueDto>>(DomainError.External(
                "github.token_missing",
                "GitHub token is not configured (Integrations:GitHub:Token)."));
        }

        string url = $"{GitHubApiBase}/repos/{Uri.EscapeDataString(repoFullName)}/issues";
        if (!string.IsNullOrWhiteSpace(state))
        {
            url += $"?state={Uri.EscapeDataString(state)}";
        }

        try
        {
            GitHubIssueWire[]? rows = await _http.GetFromJsonAsync<GitHubIssueWire[]>(
                url, JsonOptions, ct);
            if (rows is null)
            {
                return Result.Success<IReadOnlyList<GitHubIssueDto>>([]);
            }

            return Result.Success<IReadOnlyList<GitHubIssueDto>>(
                rows
                    .Where(i => i.PullRequest is null)
                    .Select(i => new GitHubIssueDto(
                        Number: i.Number,
                        Title: i.Title,
                        State: i.State,
                        Url: i.HtmlUrl,
                        Labels: i.Labels?.Select(l => l.Name ?? string.Empty).ToList()
                                 ?? new List<string>(),
                        CreatedAt: i.CreatedAt))
                .ToList());
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<IReadOnlyList<GitHubIssueDto>>(DomainError.External(
                "github.transport_error", $"GitHub API call failed: {ex.Message}"));
        }
        catch (JsonException ex)
        {
            return Result.Failure<IReadOnlyList<GitHubIssueDto>>(DomainError.External(
                "github.response_invalid", $"GitHub returned an unparseable response: {ex.Message}"));
        }
    }

    public async Task<Result<GitHubIssueDto>> CreateIssueAsync(
        string repoFullName, string title, string body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            return Result.Failure<GitHubIssueDto>(DomainError.External(
                "github.token_missing",
                "GitHub token is not configured (Integrations:GitHub:Token)."));
        }

        try
        {
            using HttpResponseMessage response = await _http.PostAsJsonAsync(
                $"{GitHubApiBase}/repos/{Uri.EscapeDataString(repoFullName)}/issues",
                new { title, body },
                JsonOptions,
                ct);
            GitHubIssueWire? created = await response.Content
                .ReadFromJsonAsync<GitHubIssueWire>(JsonOptions, ct);
            if (created is null)
            {
                return Result.Failure<GitHubIssueDto>(DomainError.External(
                    "github.create_empty",
                    "GitHub returned an empty body when creating the issue."));
            }

            return Result.Success(new GitHubIssueDto(
                Number: created.Number,
                Title: created.Title,
                State: created.State,
                Url: created.HtmlUrl,
                Labels: created.Labels?.Select(l => l.Name ?? string.Empty).ToList()
                         ?? new List<string>(),
                CreatedAt: created.CreatedAt));
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<GitHubIssueDto>(DomainError.External(
                "github.transport_error", $"GitHub API call failed: {ex.Message}"));
        }
        catch (JsonException ex)
        {
            return Result.Failure<GitHubIssueDto>(DomainError.External(
                "github.response_invalid", $"GitHub returned an unparseable response: {ex.Message}"));
        }
    }

    private sealed record GitHubBranchWire(string Name, GitHubBranchCommit? Commit, bool Protected);
    private sealed record GitHubBranchCommit(string? Sha);
    private sealed record GitHubPullRequestWire(
        int Number, string Title, string State, string? HtmlUrl,
        GitHubPullRequestRef? Head, GitHubPullRequestRef? Base, DateTimeOffset? CreatedAt);
    private sealed record GitHubPullRequestRef(string? Ref);
    private sealed record GitHubIssueWire(
        int Number, string Title, string State, string? HtmlUrl,
        IReadOnlyList<GitHubIssueLabel>? Labels, DateTimeOffset? CreatedAt,
        object? PullRequest);
    private sealed record GitHubIssueLabel(string? Name);
}
