using System.Globalization;
using System.Text.Json;
using Radzen;

namespace Cardscape.Web.Shared;

/// <summary>Typed presentation policy for activity labels and Radzen badges.</summary>
public static class ActivityPresentation
{
    public static string LabelKey(ActivityKind kind) => $"CardActivity{kind}";

    public static BadgeStyle Style(ActivityKind kind) => kind switch
    {
        ActivityKind.BoardCreated => BadgeStyle.Primary,
        ActivityKind.CardCreated or ActivityKind.CardCompleted => BadgeStyle.Success,
        ActivityKind.CardMoved or ActivityKind.CommentAdded => BadgeStyle.Info,
        ActivityKind.CardArchived => BadgeStyle.Dark,
        ActivityKind.CardDeleted => BadgeStyle.Danger,
        _ => BadgeStyle.Light
    };

    /// <summary>
    /// Human-readable summary of an activity payload: the values a person
    /// cares about (list, assignee, label, file name, due date, title)
    /// joined with " · ". Identifiers and positions are omitted; ISO
    /// timestamps are shown in the viewer's culture and time zone.
    /// </summary>
    public static string Describe(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson) || payloadJson == "{}")
        {
            return string.Empty;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            return string.Join(" · ", doc.RootElement.EnumerateObject()
                .Where(property => !IsTechnical(property.Name))
                .Select(property => FormatValue(property.Value))
                .Where(value => value.Length > 0));
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static bool IsTechnical(string key) =>
        key.EndsWith("Id", StringComparison.Ordinal)
        || key is "position" or "action" or "copiedFrom" or "mirroredFrom";

    private static string FormatValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String when value.TryGetDateTimeOffset(out DateTimeOffset at) =>
            at.LocalDateTime.ToString("g", CultureInfo.CurrentCulture),
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
        _ => string.Empty,
    };
}
