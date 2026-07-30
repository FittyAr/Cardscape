using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface ICardsApiClient
{
    Task<ApiResult<IReadOnlyList<CardSummaryDto>>> ListForBoardAsync(
        Guid boardId, bool includeArchived = false, bool includeSnoozed = false, CancellationToken ct = default);
    Task<ApiResult<CardDto>> GetAsync(Guid cardId, CancellationToken ct = default);
    Task<ApiResult<CardDto>> CreateAsync(
        Guid listId, string title, string? description, CancellationToken ct = default);
    Task<ApiResult<CardDto>> RenameAsync(Guid cardId, string newTitle, CancellationToken ct = default);
    Task<ApiResult<CardDto>> ChangeDescriptionAsync(
        Guid cardId, string newDescription, CancellationToken ct = default);
    Task<ApiResult<CardDto>> MoveAsync(
        Guid cardId, Guid newListId, double newPosition, CancellationToken ct = default);
    Task<ApiResult<CardDto>> SetDueDateAsync(
        Guid cardId, DateTimeOffset dueDate, CancellationToken ct = default);
    Task<ApiResult<CardDto>> ClearDueDateAsync(Guid cardId, CancellationToken ct = default);
    Task<ApiResult<CardDto>> CompleteAsync(Guid cardId, CancellationToken ct = default);
    Task<ApiResult<CardDto>> ReopenAsync(Guid cardId, CancellationToken ct = default);
    Task<ApiResult<CardDto>> ArchiveAsync(Guid cardId, CancellationToken ct = default);
    Task<ApiResult<CardDto>> RestoreAsync(Guid cardId, CancellationToken ct = default);
    Task<ApiResult<CardDto>> AssignAsync(Guid cardId, Guid userId, CancellationToken ct = default);
    Task<ApiResult<CardDto>> UnassignAsync(Guid cardId, Guid userId, CancellationToken ct = default);
    Task<ApiResult<CardDto>> AttachLabelAsync(Guid cardId, Guid labelId, CancellationToken ct = default);
    Task<ApiResult<CardDto>> DetachLabelAsync(Guid cardId, Guid labelId, CancellationToken ct = default);

    // G6b — Card Snooze (P3.2). Wraps the REST endpoints at
    // `/api/cards/{id}/snooze` and the board-scoped
    // `/api/cards/snoozed?boardId=...` list. The Web UI
    // toggles the snooze in `CardDetail.razor` and the
    // "show snoozed" filter in `BoardDetail.razor`.
    Task<ApiResult<DateTimeOffset>> SnoozeAsync(
        Guid cardId, DateTimeOffset until, CancellationToken ct = default);
    Task<ApiResult> UnsnoozeAsync(Guid cardId, CancellationToken ct = default);
    Task<ApiResult<IReadOnlyList<Guid>>> ListSnoozedAsync(
        Guid boardId, CancellationToken ct = default);

    // G6c — "Mirror to..." button. Wraps `POST /api/cards/{id}/mirror`
    // which dispatches the canonical `MirrorCardCommand` and returns
    // the new (mirrored) card id wrapped in `MirrorCardResultDto`.
    Task<ApiResult<MirrorCardResultDto>> MirrorToAsync(
        Guid cardId, Guid targetListId, CancellationToken ct = default);

    Task<ApiResult<IReadOnlyList<CalendarEntryDto>>> CalendarAsync(
        DateTimeOffset from, DateTimeOffset to, Guid? boardId = null, CancellationToken ct = default);
}

public sealed class CardsApiClient(IHttpClientFactory http) : ApiClientBase(http), ICardsApiClient
{
    public async Task<ApiResult<IReadOnlyList<CardSummaryDto>>> ListForBoardAsync(
        Guid boardId, bool includeArchived = false, bool includeSnoozed = false, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/cards/?boardId={boardId}&includeArchived={includeArchived}&includeSnoozed={includeSnoozed}", ct);
        return await ReadAsync<IReadOnlyList<CardSummaryDto>>(response, ct);
    }

    public async Task<ApiResult<CardDto>> GetAsync(Guid cardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync($"api/cards/{cardId}", ct);
        return await ReadAsync<CardDto>(response, ct);
    }

    public async Task<ApiResult<CardDto>> CreateAsync(
        Guid listId, string title, string? description, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/cards/",
            new CreateCardRequestDto(listId, title, description),
            ct);
        return await ReadAsync<CardDto>(response, ct);
    }

    public async Task<ApiResult<CardDto>> RenameAsync(Guid cardId, string newTitle, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/cards/{cardId}/rename",
            new { NewTitle = newTitle },
            ct);
        return await ReadAsync<CardDto>(response, ct);
    }

    public async Task<ApiResult<CardDto>> ChangeDescriptionAsync(
        Guid cardId, string newDescription, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/cards/{cardId}/description",
            new { NewDescription = newDescription },
            ct);
        return await ReadAsync<CardDto>(response, ct);
    }

    public async Task<ApiResult<CardDto>> MoveAsync(
        Guid cardId, Guid newListId, double newPosition, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/cards/{cardId}/move",
            new MoveCardRequestDto(newListId, newPosition),
            ct);
        return await ReadAsync<CardDto>(response, ct);
    }

    public async Task<ApiResult<CardDto>> SetDueDateAsync(
        Guid cardId, DateTimeOffset dueDate, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/cards/{cardId}/due-date",
            new SetCardDueDateRequestDto(dueDate),
            ct);
        return await ReadAsync<CardDto>(response, ct);
    }

    public async Task<ApiResult<CardDto>> ClearDueDateAsync(Guid cardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync($"api/cards/{cardId}/due-date", ct);
        return await ReadAsync<CardDto>(response, ct);
    }

    public async Task<ApiResult<CardDto>> CompleteAsync(Guid cardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync($"api/cards/{cardId}/complete", content: null, ct);
        return await ReadAsync<CardDto>(response, ct);
    }

    public async Task<ApiResult<CardDto>> ReopenAsync(Guid cardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync($"api/cards/{cardId}/reopen", content: null, ct);
        return await ReadAsync<CardDto>(response, ct);
    }

    public async Task<ApiResult<CardDto>> ArchiveAsync(Guid cardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync($"api/cards/{cardId}/archive", content: null, ct);
        return await ReadAsync<CardDto>(response, ct);
    }

    public async Task<ApiResult<CardDto>> RestoreAsync(Guid cardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync($"api/cards/{cardId}/restore", content: null, ct);
        return await ReadAsync<CardDto>(response, ct);
    }

    public async Task<ApiResult<CardDto>> AssignAsync(Guid cardId, Guid userId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync($"api/cards/{cardId}/assign/{userId}", content: null, ct);
        return await ReadAsync<CardDto>(response, ct);
    }

    public async Task<ApiResult<CardDto>> UnassignAsync(Guid cardId, Guid userId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync($"api/cards/{cardId}/assign/{userId}", ct);
        return await ReadAsync<CardDto>(response, ct);
    }

    public async Task<ApiResult<CardDto>> AttachLabelAsync(Guid cardId, Guid labelId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync($"api/cards/{cardId}/labels/{labelId}", content: null, ct);
        return await ReadAsync<CardDto>(response, ct);
    }

    public async Task<ApiResult<CardDto>> DetachLabelAsync(Guid cardId, Guid labelId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync($"api/cards/{cardId}/labels/{labelId}", ct);
        return await ReadAsync<CardDto>(response, ct);
    }

    public async Task<ApiResult<DateTimeOffset>> SnoozeAsync(
        Guid cardId, DateTimeOffset until, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/cards/{cardId}/snooze",
            new SnoozeCardRequestDto(until),
            ct);
        return await ReadAsync<DateTimeOffset>(response, ct);
    }

    public async Task<ApiResult> UnsnoozeAsync(Guid cardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync(
            $"api/cards/{cardId}/snooze", ct);
        return await ReadAsync(response, ct);
    }

    public async Task<ApiResult<IReadOnlyList<Guid>>> ListSnoozedAsync(
        Guid boardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/cards/snoozed?boardId={boardId}", ct);
        return await ReadAsync<IReadOnlyList<Guid>>(response, ct);
    }

    public async Task<ApiResult<MirrorCardResultDto>> MirrorToAsync(
        Guid cardId, Guid targetListId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/cards/{cardId}/mirror",
            new { TargetListId = targetListId },
            ct);
        return await ReadAsync<MirrorCardResultDto>(response, ct);
    }

    public async Task<ApiResult<IReadOnlyList<CalendarEntryDto>>> CalendarAsync(
        DateTimeOffset from, DateTimeOffset to, Guid? boardId = null, CancellationToken ct = default)
    {
        string fromStr = Uri.EscapeDataString(from.ToString("o"));
        string toStr = Uri.EscapeDataString(to.ToString("o"));
        string url = $"api/cards/calendar?from={fromStr}&to={toStr}";
        if (boardId is Guid bid)
        {
            url += $"&boardId={bid}";
        }

        HttpResponseMessage response = await CreateClient().GetAsync(url, ct);
        return await ReadAsync<IReadOnlyList<CalendarEntryDto>>(response, ct);
    }
}
