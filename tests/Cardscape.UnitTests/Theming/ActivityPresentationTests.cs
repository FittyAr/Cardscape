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
    [InlineData(ActivityKind.CardCompleted, "CardActivityCardCompleted", BadgeStyle.Success)]
    [InlineData(ActivityKind.CardReopened, "CardActivityCardReopened", BadgeStyle.Light)]
    [InlineData(ActivityKind.CardDescriptionChanged, "CardActivityCardDescriptionChanged", BadgeStyle.Light)]
    [InlineData(ActivityKind.CardDeleted, "CardActivityCardDeleted", BadgeStyle.Danger)]
    [InlineData(ActivityKind.CommentEdited, "CardActivityCommentEdited", BadgeStyle.Light)]
    [InlineData(ActivityKind.CommentDeleted, "CardActivityCommentDeleted", BadgeStyle.Light)]
    [InlineData(ActivityKind.LabelCreated, "CardActivityLabelCreated", BadgeStyle.Light)]
    [InlineData(ActivityKind.LabelUpdated, "CardActivityLabelUpdated", BadgeStyle.Light)]
    [InlineData(ActivityKind.LabelDeleted, "CardActivityLabelDeleted", BadgeStyle.Light)]
    [InlineData(ActivityKind.ChecklistItemAdded, "CardActivityChecklistItemAdded", BadgeStyle.Light)]
    [InlineData(ActivityKind.ChecklistItemRenamed, "CardActivityChecklistItemRenamed", BadgeStyle.Light)]
    [InlineData(ActivityKind.ChecklistItemDeleted, "CardActivityChecklistItemDeleted", BadgeStyle.Light)]
    public void KnownActivity_HasExactLocalizedKeyAndRadzenStyle(
        ActivityKind kind, string labelKey, BadgeStyle style)
    {
        ActivityPresentation.LabelKey(kind).Should().Be(labelKey);
        ActivityPresentation.Style(kind).Should().Be(style);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("{}", "")]
    [InlineData("not json", "")]
    [InlineData("""{"listId":"5f1c","listName":"Review","position":3}""", "Review")]
    [InlineData("""{"userId":"5f1c","userName":"Ada Lovelace"}""", "Ada Lovelace")]
    [InlineData("""{"labelId":"5f1c","name":"Bug","attachmentId":"x","fileName":"notes.md"}""", "Bug · notes.md")]
    public void Describe_ShowsHumanValuesAndHidesIdentifiers(string? payload, string expected) =>
        ActivityPresentation.Describe(payload).Should().Be(expected);

    [Fact]
    public void Describe_FormatsTimestampsInTheViewerCulture()
    {
        DateTimeOffset due = new(2026, 10, 3, 17, 0, 0, TimeSpan.Zero);

        ActivityPresentation.Describe($$"""{"dueDate":"{{due:O}}"}""")
            .Should().Be(due.LocalDateTime.ToString("g", System.Globalization.CultureInfo.CurrentCulture));
    }
}
