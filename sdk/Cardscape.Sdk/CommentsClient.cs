using System.Net.Http;

namespace Cardscape.Sdk;

/// <summary>Provides operations for comments attached to cards.</summary>
public sealed class CommentsClient
{
    private readonly CardscapeClient _parent;

    /// <summary>Initializes a new instance of the <see cref="CommentsClient"/> class.</summary>
    /// <param name="parent">The client that supplies transport and serialization.</param>
    public CommentsClient(CardscapeClient parent) => _parent = parent;

    /// <summary>Lists the comments attached to a card.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The card comments.</returns>
    public Task<IReadOnlyList<CommentDto>> ListAsync(Guid cardId, CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<CommentDto>>(
            new HttpRequestMessage(HttpMethod.Get, $"api/cards/{cardId}/comments"), ct);

    /// <summary>Adds a comment to a card.</summary>
    /// <param name="cardId">The card identifier.</param>
    /// <param name="body">The comment content.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The created comment.</returns>
    public Task<CommentDto> AddAsync(Guid cardId, AddCommentRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, $"api/cards/{cardId}/comments") { Content = _parent.CreateJsonContent(body) };
        return _parent.SendAsync<CommentDto>(req, ct);
    }
}
