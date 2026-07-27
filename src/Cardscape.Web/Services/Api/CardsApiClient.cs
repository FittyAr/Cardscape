using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface ICardsApiClient
{
    Task<ApiResult<IReadOnlyList<CardSummaryDto>>> ListForBoardAsync(
        Guid boardId, bool includeArchived = false, CancellationToken ct = default);
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
}

public sealed class CardsApiClient(IHttpClientFactory http) : ApiClientBase(http), ICardsApiClient
{
    public async Task<ApiResult<IReadOnlyList<CardSummaryDto>>> ListForBoardAsync(
        Guid boardId, bool includeArchived = false, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/cards/?boardId={boardId}&includeArchived={includeArchived}", ct);
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
}
