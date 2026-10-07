using System.Globalization;
using System.Text;
using System.Text.Json;
using Cardscape.Application.Abstractions.Storage;
using Cardscape.Seeder.Company;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;
using Cardscape.Seeder.Simulation;
using Microsoft.Extensions.DependencyInjection;

namespace Cardscape.Seeder.Steps;

/// <summary>
/// Fills in what a card accumulates while it is worked on: checklists
/// ticked off in line with the card's stage, custom-field values of every
/// kind, recurring chores, per-card aging overrides, real attachment files
/// (stored through the configured <see cref="IStorageService"/>, so the
/// download links work) and cross-board mirrors.
/// </summary>
internal sealed class CardDetailsSeedStep : SeedStepBase
{
    private static readonly (string Title, int IntervalDays)[] RecurringChores =
    [
        ("Quarterly tax filing prep", 90),
        ("Vendor security review: Sentry", 180),
        ("Top-10 tickets of the month", 30),
        ("Insurance renewal: cyber liability", 365),
    ];

    private static readonly (string Source, string Target)[] Mirrors =
    [
        ("Triage P1 from last week's release", "Customer Support"),
        ("Component: empty-state pattern", "Product Discovery"),
    ];

    public override string Name => "Card details: checklists, custom fields, attachments, mirrors";
    public override int Order => 60;

    public override async Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        IStorageService storage = context.Services.GetRequiredService<IStorageService>();
        SeedTimeline timeline = context.Timeline;

        foreach ((Card card, int index) in context.Cards.Select((card, index) => (card, index)).ToList())
        {
            Board board = context.BoardOf(card);
            CardStage stage = CardStageExtensions.FromListName(context.ListOf(card).Name.Value);

            if (index % 5 is 0 or 2)
            {
                PlantChecklist(context, board, card, stage, index);
            }

            PlantCustomFieldValues(context, board, card, stage);

            if (index % 6 == 1)
            {
                await PlantAttachmentAsync(context, storage, board, card, index, cancellationToken);
            }

            if (stage == CardStage.Backlog && LastTouched(card) < context.Now.AddDays(-30) && timeline.Chance(0.5))
            {
                CardAgingSettings aging = CardAgingSettings.Create(card.Id, CardAgingMode.ByActivity, 10, card.CreatedAt).Value;
                context.Track(context.CardAgingSettings, aging);
            }
        }

        PlantRecurrences(context);
        PlantMirrors(context);

        Log(log, SeedLogLevel.Success,
            $"Inserted {context.Checklists.Count} checklists ({context.ChecklistItems.Count(i => i.IsCompleted)}/{context.ChecklistItems.Count} items done), " +
            $"{context.CustomFieldValues.Count} custom field values, {context.Attachments.Count} attachments, " +
            $"{context.CardRecurrences.Count} recurrences, {context.CardAgingSettings.Count} aging overrides and {context.CardMirrors.Count} mirrors.");
    }

    private static void PlantChecklist(SeedContext context, Board board, Card card, CardStage stage, int index)
    {
        (string title, IReadOnlyList<string> items) = NexoraStudios.Checklists[index % NexoraStudios.Checklists.Count];
        User owner = context.Users.First(u => card.Members.Any(m => m.UserId == u.Id.Value));
        DateTimeOffset at = card.CreatedAt.AddHours(2);

        Checklist checklist = Checklist.Create(ChecklistId.New(), card.Id, ChecklistTitle.Create(title).Value, owner.Id.Value, at).Value;
        context.RecordActivity(board, card.Id.Value, owner, ActivityKind.ChecklistCreated, at, new { checklistId = checklist.Id.Value, title });

        int done = (int)Math.Round(items.Count * stage.ChecklistProgress());
        for (int i = 0; i < items.Count; i++)
        {
            ChecklistItem item = checklist.AddItem(ChecklistItemText.Create(items[i]).Value, Position.From(i + 1), at);
            if (i < done)
            {
                DateTimeOffset checkedAt = context.Timeline.Between(at, LastTouched(card) > at ? LastTouched(card) : null);
                checklist.CheckItem(item.Id, checkedAt);
                context.RecordActivity(board, card.Id.Value, owner, ActivityKind.ChecklistItemCompleted, checkedAt,
                    new { checklistId = checklist.Id.Value, itemId = item.Id.Value });
            }
        }

        context.Track(context.Checklists, checklist);
        context.ChecklistItems.AddRange(checklist.Items);
    }

    private static void PlantCustomFieldValues(SeedContext context, Board board, Card card, CardStage stage)
    {
        SeedTimeline timeline = context.Timeline;
        foreach (CustomFieldDefinition definition in context.CustomFieldDefinitions.Where(d => d.BoardId == board.Id))
        {
            if (!timeline.Chance(0.75))
            {
                continue;
            }

            object value = definition.Kind switch
            {
                CustomFieldKind.Dropdown => timeline.Pick(JsonSerializer.Deserialize<string[]>(definition.OptionsJson) ?? ["Medium"]),
                CustomFieldKind.Number => (object)timeline.Pick([1, 2, 3, 5, 8, 13]),
                CustomFieldKind.Date => (card.DueDate ?? context.Now.AddDays(timeline.Next(7, 60)))
                    .UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                CustomFieldKind.Checkbox => stage != CardStage.Backlog && timeline.Chance(0.5),
                _ => $"https://docs.nexora.example/specs/{card.Id.Value.ToString("N")[..8]}",
            };
            CustomFieldValue fieldValue = CustomFieldValue.Create(
                definition.Id, card.Id, JsonSerializer.Serialize(value), card.CreatedAt.AddHours(3)).Value;
            context.Track(context.CustomFieldValues, fieldValue);
        }
    }

    private static async Task PlantAttachmentAsync(
        SeedContext context, IStorageService storage, Board board, Card card, int index, CancellationToken cancellationToken)
    {
        (string fileName, string mimeType, string content) = (index % 3) switch
        {
            0 => ("notes.md", "text/markdown", $"# {card.Title.Value}\n\nMeeting notes and decisions for this card.\n\n- Agreed scope\n- Open questions\n"),
            1 => ("metrics.csv", "text/csv", "week,opened,closed\n36,14,11\n37,18,16\n38,9,15\n39,12,12\n"),
            _ => ("diagram.svg", "image/svg+xml", """<svg xmlns="http://www.w3.org/2000/svg" width="240" height="120"><rect x="10" y="10" width="100" height="60" rx="8" fill="#0079bf"/><rect x="130" y="50" width="100" height="60" rx="8" fill="#61bd4f"/></svg>"""),
        };

        byte[] bytes = Encoding.UTF8.GetBytes(content);
        string storageKey = $"cards/{card.Id.Value:N}/{Guid.NewGuid():N}/{fileName}";
        await using (var stream = new MemoryStream(bytes))
        {
            await storage.SaveAsync(storageKey, stream, mimeType, cancellationToken);
        }

        User uploader = context.Users.First(u => card.Members.Any(m => m.UserId == u.Id.Value));
        DateTimeOffset at = context.Timeline.Between(card.CreatedAt, LastTouched(card) > card.CreatedAt ? LastTouched(card) : null);
        Attachment attachment = Attachment.Create(
            AttachmentId.New(), card.Id, fileName, mimeType, bytes.LongLength, storageKey, uploader.Id.Value, at).Value;
        context.Track(context.Attachments, attachment);
        context.RecordActivity(board, card.Id.Value, uploader, ActivityKind.AttachmentAdded, at,
            new { attachmentId = attachment.Id.Value, fileName });
    }

    private static void PlantRecurrences(SeedContext context)
    {
        foreach ((string title, int intervalDays) in RecurringChores)
        {
            if (context.Cards.FirstOrDefault(c => c.Title.Value == title) is not { } card)
            {
                continue;
            }

            DateTimeOffset next = card.DueDate is { } due && due > context.Now ? due : context.Now.AddDays(intervalDays / 3.0);
            CardRecurrence recurrence = CardRecurrence.Create(
                CardRecurrenceId.New(), card.Id, intervalDays, next, card.CreatedBy ?? context.WorkspaceOwnerId, card.CreatedAt).Value;
            context.Track(context.CardRecurrences, recurrence);
        }
    }

    private static void PlantMirrors(SeedContext context)
    {
        foreach ((string sourceTitle, string targetBoardName) in Mirrors)
        {
            if (context.Cards.FirstOrDefault(c => c.Title.Value == sourceTitle) is not { } source)
            {
                continue;
            }

            Board target = context.Boards.First(b => b.Name.Value == targetBoardName);
            BoardList backlog = context.List(target, CardStage.Backlog.ListName());
            User mirroredBy = context.User(SetupOwnerKey(target));
            DateTimeOffset at = context.Timeline.Between(source.CreatedAt);

            Card mirror = Card.Create(
                CardId.New(), backlog.Id, source.Title, source.Description, Position.From(0.5), mirroredBy.Id.Value, at).Value;
            context.Track(context.Cards, mirror);
            context.RecordActivity(target, mirror.Id.Value, mirroredBy, ActivityKind.CardCreated, at,
                new { title = source.Title.Value, mirroredFrom = source.Id.Value });

            CardMirror link = CardMirror.Create(source.Id, mirror.Id, backlog.Id, at, mirroredBy.Id.Value).Value;
            context.Track(context.CardMirrors, link);
        }
    }

    private static DateTimeOffset LastTouched(Card card) => card.UpdatedAt ?? card.CreatedAt;

    private static string SetupOwnerKey(Board board) => BoardSetupSeedStep.Blueprint(board).OwnerKey;
}
