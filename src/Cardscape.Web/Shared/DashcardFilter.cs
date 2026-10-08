using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cardscape.Web.Shared;

/// <summary>
/// Optional scope of a dashboard widget, stored in the dashcard's
/// <c>ConfigurationJson</c> (e.g. <c>{"listId":"…","labelId":"…"}</c>):
/// the widget only counts cards in that list, with that label and/or
/// assigned to that person. An empty filter (<c>{}</c>) counts the whole board.
/// </summary>
public sealed record DashcardFilter(Guid? ListId = null, Guid? LabelId = null, Guid? MemberId = null)
{
    public static readonly DashcardFilter None = new();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [JsonIgnore]
    public bool IsEmpty => ListId is null && LabelId is null && MemberId is null;

    /// <summary>Reads a stored configuration; anything unreadable counts as no filter.</summary>
    public static DashcardFilter Parse(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return None;
        }

        try
        {
            return JsonSerializer.Deserialize<DashcardFilter>(configurationJson, Json) ?? None;
        }
        catch (JsonException)
        {
            return None;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public bool Matches(CardSummaryDto card) =>
        (ListId is not { } listId || card.ListId == listId)
        && (LabelId is not { } labelId || (card.Labels ?? []).Any(label => label.Id == labelId))
        && (MemberId is not { } memberId || (card.Members ?? []).Any(member => member.UserId == memberId));
}
