using System.Net.Http;

namespace Cardscape.Sdk;

/// <summary>Provides operations for cards and their workflow state.</summary>
public sealed class CardsClient
{
    private readonly CardscapeClient _parent;

    /// <summary>Initializes a new instance of the <see cref="CardsClient"/> class.</summary>
    /// <param name="parent">The client that supplies transport and serialization.</param>
    public CardsClient(CardscapeClient parent) => _parent = parent;

    /// <summary>Lists the cards in a list.</summary>
    /// <param name="listId">The list identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The ordered cards in the list.</returns>
    public Task<IReadOnlyList<CardDto>> ListAsync(Guid listId, CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<CardDto>>(
            new HttpRequestMessage(HttpMethod.Get, $"api/lists/{listId}/cards"), ct);

    /// <summary>Gets a card by identifier.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The requested card.</returns>
    public Task<CardDto> GetAsync(Guid cardId, CancellationToken ct = default) =>
        _parent.SendAsync<CardDto>(new HttpRequestMessage(HttpMethod.Get, $"api/cards/{cardId}"), ct);

    /// <summary>Creates a card.</summary>
    /// <param name="body">The card creation values.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The created card.</returns>
    public Task<CardDto> CreateAsync(CreateCardRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, "api/cards/") { Content = _parent.CreateJsonContent(body) };
        return _parent.SendAsync<CardDto>(req, ct);
    }

    /// <summary>Updates the mutable fields of a card.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="body">The replacement values.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The updated card.</returns>
    public Task<CardDto> UpdateAsync(Guid cardId, UpdateCardRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Put, $"api/cards/{cardId}") { Content = _parent.CreateJsonContent(body) };
        return _parent.SendAsync<CardDto>(req, ct);
    }

    /// <summary>Moves a card to a list and ordering position.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="body">The destination list and ordering value.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The moved card.</returns>
    public Task<CardDto> MoveAsync(Guid cardId, MoveCardRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, $"api/cards/{cardId}/move") { Content = _parent.CreateJsonContent(body) };
        return _parent.SendAsync<CardDto>(req, ct);
    }

    /// <summary>Marks a card as completed.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task CompleteAsync(Guid cardId, CancellationToken ct = default) =>
        _parent.SendAsync<object>(new HttpRequestMessage(HttpMethod.Post, $"api/cards/{cardId}/complete"), ct);

    /// <summary>Marks a completed card as open.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ReopenAsync(Guid cardId, CancellationToken ct = default) =>
        _parent.SendAsync<object>(new HttpRequestMessage(HttpMethod.Post, $"api/cards/{cardId}/reopen"), ct);

    /// <summary>Archives a card.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ArchiveAsync(Guid cardId, CancellationToken ct = default) =>
        _parent.SendAsync<object>(new HttpRequestMessage(HttpMethod.Post, $"api/cards/{cardId}/archive"), ct);

    /// <summary>Restores an archived card.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task RestoreAsync(Guid cardId, CancellationToken ct = default) =>
        _parent.SendAsync<object>(new HttpRequestMessage(HttpMethod.Post, $"api/cards/{cardId}/restore"), ct);

    /// <summary>Assigns a user to a card.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task AssignAsync(Guid cardId, Guid userId, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, $"api/cards/{cardId}/assign")
        {
            Content = _parent.CreateJsonContent(new { userId })
        };
        return _parent.SendAsync<object>(req, ct);
    }

    /// <summary>Attaches a board label to a card.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="labelId">The label identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task AttachLabelAsync(Guid cardId, Guid labelId, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, $"api/cards/{cardId}/labels")
        {
            Content = _parent.CreateJsonContent(new { labelId })
        };
        return _parent.SendAsync<object>(req, ct);
    }
}
