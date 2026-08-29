using Cardscape.Application.Abstractions.Search;
using Wolverine;

namespace Cardscape.Application.Search;

public sealed record SearchHitDto(
    string Id,
    SearchHitKind Kind,
    string Title,
    string Snippet,
    Guid? BoardId,
    Guid? CardId,
    string Url,
    double Score);

public sealed record SearchPageDto(
    IReadOnlyList<SearchHitDto> Items,
    int Total);

public sealed record SearchQuery(
    string Query,
    Guid? BoardId = null,
    SearchHitKind? Kind = null,
    int Page = 1,
    int PageSize = 20) : IMessage;
