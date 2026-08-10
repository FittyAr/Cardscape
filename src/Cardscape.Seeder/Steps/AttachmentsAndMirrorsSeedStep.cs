using Cardscape.Domain.Attachments;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Lists;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;

namespace Cardscape.Seeder.Steps;

/// <summary>One file attachment per board and a handful of
/// card-mirror rows. The attachment uses a fake storage key
/// because the local-filesystem <c>IStorageService</c> is
/// never called during a seed; we only plant the metadata
/// row.</summary>
public sealed class AttachmentsAndMirrorsSeedStep : SeedStepBase
{
    private static readonly (string FileName, string Mime, long Size)[] SampleFiles =
    {
        ("spec.pdf", "application/pdf", 184_320L),
        ("screenshot.png", "image/png", 2_457_600L),
        ("wireframe.fig", "application/octet-stream", 1_843_200L),
        ("recording.mp4", "video/mp4", 12_582_912L),
        ("notes.md", "text/markdown", 4_096L),
    };

    public override string Name => "Attachments + card mirrors";
    public override int Order => 80;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        DateTimeOffset now = context.Now;
        var random = new Random(303);
        User uploader = context.Users[0];

        // 1. Attachments: one per board, attached to a random
        //    card in that board. Uses the first persona as the
        //    uploader so the audit log shows the right person.
        foreach (Board board in context.Boards)
        {
            List<Card> boardCards = context.Cards
                .Where(c => context.Lists.Any(l => l.Id.Value == c.ListId.Value && l.BoardId.Value == board.Id.Value))
                .ToList();
            if (boardCards.Count == 0)
            {
                continue;
            }

            Card hostCard = boardCards[random.Next(0, boardCards.Count)];
            (string fileName, string mime, long size) = SampleFiles[random.Next(0, SampleFiles.Length)];
            string storageKey = $"seed/{board.Id.Value:N}/{fileName}";

            Result<Attachment> attachment = Attachment.Create(
                AttachmentId.New(),
                hostCard.Id,
                fileName,
                mime,
                size,
                storageKey,
                uploader.Id.Value,
                now.AddDays(-1));
            if (attachment.IsSuccess)
            {
                context.Db.Attachments.Add(attachment.Value);
                context.Attachments.Add(attachment.Value);
            }
        }

        // 2. Card mirrors: take a Done-list card on the
        //    Engineering board and mirror it onto the same
        //    list on the Customer Support board. Demonstrates
        //    the cross-board link without exploding the row
        //    count.
        if (context.Boards.Count >= 2 && context.Cards.Count >= 6)
        {
            Board source = context.Boards[0];
            Board target = context.Boards[context.Boards.Count - 1];
            BoardList? sourceList = context.Lists.FirstOrDefault(l => l.BoardId.Value == source.Id.Value && l.Name.Value == "Done");
            BoardList? targetList = context.Lists.FirstOrDefault(l => l.BoardId.Value == target.Id.Value && l.Name.Value == "Backlog");
            if (sourceList is not null && targetList is not null)
            {
                Card sourceCard = context.Cards.FirstOrDefault(c => c.ListId.Value == sourceList.Id.Value) ?? context.Cards[0];
                Card mirroredCard = Card.Create(
                    CardId.New(),
                    targetList.Id,
                    Cardscape.Domain.Cards.CardTitle.Create($"[Mirror] {sourceCard.Title.Value}").Value,
                    sourceCard.Description,
                    Cardscape.Domain.Common.Position.From(99),
                    context.WorkspaceOwnerId,
                    now).Value;
                context.Db.Cards.Add(mirroredCard);
                context.Cards.Add(mirroredCard);

                Result<CardMirror> mirror = CardMirror.Create(
                    sourceCard.Id,
                    mirroredCard.Id,
                    targetList.Id,
                    now,
                    context.WorkspaceOwnerId);
                if (mirror.IsSuccess)
                {
                    context.Db.CardMirrors.Add(mirror.Value);
                    context.CardMirrors.Add(mirror.Value);
                }
            }
        }

        Log(log, SeedLogLevel.Success,
            $"Inserted {context.Attachments.Count} attachments and {context.CardMirrors.Count} card mirrors.");
        return Task.CompletedTask;
    }
}
