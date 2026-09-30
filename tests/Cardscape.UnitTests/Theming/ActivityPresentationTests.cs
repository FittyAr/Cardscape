using Cardscape.Web.Shared;
using Radzen;

namespace Cardscape.UnitTests.Theming;

public sealed class ActivityPresentationTests
{
    [Theory]
    [InlineData(ActivityKind.BoardCreated, "CardActivityBoardCreated", BadgeStyle.Primary)]
    [InlineData(ActivityKind.BoardRenamed, "CardActivityBoardRenamed", BadgeStyle.Light)]
    [InlineData(ActivityKind.BoardArchived, "CardActivityBoardArchived", BadgeStyle.Light)]
    [InlineData(ActivityKind.BoardUnarchived, "CardActivityBoardUnarchived", BadgeStyle.Light)]
    [InlineData(ActivityKind.ListCreated, "CardActivityListCreated", BadgeStyle.Light)]
    [InlineData(ActivityKind.ListRenamed, "CardActivityListRenamed", BadgeStyle.Light)]
    [InlineData(ActivityKind.ListMoved, "CardActivityListMoved", BadgeStyle.Light)]
    [InlineData(ActivityKind.ListArchived, "CardActivityListArchived", BadgeStyle.Light)]
    [InlineData(ActivityKind.CardCreated, "CardActivityCardCreated", BadgeStyle.Success)]
    [InlineData(ActivityKind.CardRenamed, "CardActivityCardRenamed", BadgeStyle.Light)]
    [InlineData(ActivityKind.CardMoved, "CardActivityCardMoved", BadgeStyle.Info)]
    [InlineData(ActivityKind.CardArchived, "CardActivityCardArchived", BadgeStyle.Dark)]
    [InlineData(ActivityKind.CardRestored, "CardActivityCardRestored", BadgeStyle.Light)]
    [InlineData(ActivityKind.CardAssigned, "CardActivityCardAssigned", BadgeStyle.Light)]
    [InlineData(ActivityKind.CardUnassigned, "CardActivityCardUnassigned", BadgeStyle.Light)]
    [InlineData(ActivityKind.CardDueDateSet, "CardActivityCardDueDateSet", BadgeStyle.Light)]
    [InlineData(ActivityKind.CardDueDateCleared, "CardActivityCardDueDateCleared", BadgeStyle.Light)]
    [InlineData(ActivityKind.LabelAdded, "CardActivityLabelAdded", BadgeStyle.Light)]
    [InlineData(ActivityKind.LabelRemoved, "CardActivityLabelRemoved", BadgeStyle.Light)]
    [InlineData(ActivityKind.CommentAdded, "CardActivityCommentAdded", BadgeStyle.Info)]
    [InlineData(ActivityKind.ChecklistCreated, "CardActivityChecklistCreated", BadgeStyle.Light)]
    [InlineData(ActivityKind.ChecklistItemCompleted, "CardActivityChecklistItemCompleted", BadgeStyle.Light)]
    [InlineData(ActivityKind.ChecklistItemUncompleted, "CardActivityChecklistItemUncompleted", BadgeStyle.Light)]
    [InlineData(ActivityKind.AttachmentAdded, "CardActivityAttachmentAdded", BadgeStyle.Light)]
    [InlineData(ActivityKind.AttachmentRemoved, "CardActivityAttachmentRemoved", BadgeStyle.Light)]
    public void KnownActivity_HasExactLocalizedKeyAndRadzenStyle(
        ActivityKind kind, string labelKey, BadgeStyle style)
    {
        ActivityPresentation.LabelKey(kind).Should().Be(labelKey);
        ActivityPresentation.Style(kind).Should().Be(style);
    }
}
