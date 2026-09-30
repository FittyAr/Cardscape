using Radzen;

namespace Cardscape.Web.Shared;

/// <summary>Typed presentation policy for activity labels and Radzen badges.</summary>
public static class ActivityPresentation
{
    public static string LabelKey(ActivityKind kind) => $"CardActivity{kind}";

    public static BadgeStyle Style(ActivityKind kind) => kind switch
    {
        ActivityKind.BoardCreated => BadgeStyle.Primary,
        ActivityKind.CardCreated => BadgeStyle.Success,
        ActivityKind.CardMoved or ActivityKind.CommentAdded => BadgeStyle.Info,
        ActivityKind.CardArchived => BadgeStyle.Dark,
        _ => BadgeStyle.Light
    };
}
