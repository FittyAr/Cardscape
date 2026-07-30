using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Import;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Import;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Workspaces;
using static Cardscape.Domain.Boards.Errors.BoardErrors;

namespace Cardscape.Infrastructure.Import;

/// <summary>
/// Default <see cref="IImportService"/> that parses a Trello
/// <c>boards.json</c> archive. The file is an array of Trello
/// board objects; each board has nested <c>lists</c>,
/// <c>cards</c>, <c>labels</c>, and <c>members</c>. We map
/// the structure to a Cardscape board (one Trello board per
/// Cardscape board) inside the supplied target workspace.
///
/// Supports a dry-run mode via <c>previewOnly</c>: the
/// service still parses the file, builds the in-memory
/// aggregate tree, and returns an <see cref="ImportPreview"/>
/// summary — it just skips every <c>AddAsync</c> call and
/// the final <c>SaveChangesAsync</c>, so the database is
/// untouched.
/// </summary>
public sealed class TrelloImportService(
    IRepository<Workspace, WorkspaceId> workspaces,
    IBoardRepository boards,
    IBoardListRepository lists,
    ICardRepository cards,
    ILabelRepository labels,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser) : IImportService
{
    // Sample size caps for the preview summary. Trello exports
    // can be huge; we don't want to push a 5,000-name list
    // through the REST/MCP/UI surface.
    private const int MaxSampleBoardNames = 5;
    private const int MaxSampleListNames = 10;
    private const int MaxSampleCardNames = 20;

    public async Task<Result<ImportResult>> ImportTrelloJsonAsync(
        Stream json,
        Guid targetWorkspaceId,
        bool previewOnly = false,
        CancellationToken ct = default)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<ImportResult>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(targetWorkspaceId), ct);
        if (workspace is null)
        {
            return Result.Failure<ImportResult>(DomainError.NotFound(
                "imports.workspace_not_found",
                "Target workspace was not found."));
        }

        if (!workspace.HasMember(currentUser.Id.Value))
        {
            return Result.Failure<ImportResult>(NotMember);
        }

        TrelloBoard[]? trelloBoards;
        try
        {
            trelloBoards = await JsonSerializer.DeserializeAsync<TrelloBoard[]>(
                json, JsonOptions, ct);
        }
        catch (JsonException ex)
        {
            return Result.Failure<ImportResult>(DomainError.Validation(
                "imports.invalid_json",
                $"Trello export is not valid JSON: {ex.Message}"));
        }

        if (trelloBoards is null || trelloBoards.Length == 0)
        {
            return Result.Failure<ImportResult>(DomainError.Validation(
                "imports.empty_archive",
                "Trello export contains no boards."));
        }

        var importedBoardIds = new List<Guid>();
        var importedListIds = new List<Guid>();
        var importedCardIds = new List<Guid>();
        var importedLabelIds = new List<Guid>();

        // Preview summary accumulators. Always populated — the
        // apply path also surfaces a preview so the UI can
        // show "what was created" without a second round-trip.
        int boardCount = 0;
        int listCount = 0;
        int cardCount = 0;
        int labelCount = 0;
        int memberCount = 0;
        var sampleBoardNames = new List<string>(MaxSampleBoardNames);
        var sampleListNames = new List<string>(MaxSampleListNames);
        var sampleCardNames = new List<string>(MaxSampleCardNames);

        foreach (var tb in trelloBoards)
        {
            ct.ThrowIfCancellationRequested();

            var boardName = string.IsNullOrWhiteSpace(tb.Name) ? "Imported board" : tb.Name;
            var nameResult = BoardName.Create(boardName);
            if (nameResult.IsFailure)
            {
                continue;
            }

            var descResult = BoardDescription.Create(tb.Desc ?? string.Empty);
            if (descResult.IsFailure)
            {
                continue;
            }

            var boardResult = Board.Create(
                BoardId.New(),
                new WorkspaceId(targetWorkspaceId),
                nameResult.Value,
                descResult.Value,
                BoardVisibility.Workspace,
                currentUser.Id.Value,
                clock.UtcNow);

            if (boardResult.IsFailure)
            {
                continue;
            }

            boardCount++;
            if (sampleBoardNames.Count < MaxSampleBoardNames)
            {
                sampleBoardNames.Add(boardName);
            }

            // Members: counted for the preview only — Trello
            // member accounts don't map 1:1 to Cardscape users
            // (they may not exist yet), so we don't persist them.
            int boardMemberCount = 0;
            if (tb.Members is not null)
            {
                foreach (var _ in tb.Members)
                {
                    boardMemberCount++;
                }
            }
            memberCount += boardMemberCount;

            if (!previewOnly)
            {
                await boards.AddAsync(boardResult.Value, ct);
                importedBoardIds.Add(boardResult.Value.Id.Value);
            }

            // Labels first (cards reference them by id).
            var labelMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var tl in tb.Labels ?? [])
            {
                if (string.IsNullOrWhiteSpace(tl.Name) && string.IsNullOrWhiteSpace(tl.Color))
                {
                    continue;
                }

                var labelNameResult = LabelName.Create(string.IsNullOrWhiteSpace(tl.Name) ? tl.Color : tl.Name);
                if (labelNameResult.IsFailure)
                {
                    continue;
                }

                var colorValue = NormalizeTrelloColor(tl.Color);
                var colorResult = Color.Create(colorValue);
                if (colorResult.IsFailure)
                {
                    colorResult = Color.Create(Color.Palette.Gray.Value);
                }

                var labelResult = Label.Create(
                    LabelId.New(),
                    boardResult.Value.Id,
                    labelNameResult.Value,
                    colorResult.Value,
                    currentUser.Id.Value,
                    clock.UtcNow);

                if (labelResult.IsFailure)
                {
                    continue;
                }

                labelCount++;
                if (!previewOnly)
                {
                    await labels.AddAsync(labelResult.Value, ct);
                    labelMap[tl.Id] = labelResult.Value.Id.Value;
                    importedLabelIds.Add(labelResult.Value.Id.Value);
                }
            }

            // Lists.
            var listMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            int listIndex = 0;
            foreach (var tl in tb.Lists ?? [])
            {
                var listName = string.IsNullOrWhiteSpace(tl.Name) ? "Imported" : tl.Name;
                var listNameResult = ListName.Create(listName);
                if (listNameResult.IsFailure)
                {
                    listIndex++;
                    continue;
                }

                var position = Position.From(listIndex * 1024);
                var listResult = BoardList.Create(
                    BoardListId.New(),
                    boardResult.Value.Id,
                    listNameResult.Value,
                    position,
                    currentUser.Id.Value,
                    clock.UtcNow);

                if (listResult.IsFailure)
                {
                    listIndex++;
                    continue;
                }

                listCount++;
                if (sampleListNames.Count < MaxSampleListNames)
                {
                    sampleListNames.Add(listName);
                }

                if (!previewOnly)
                {
                    await lists.AddAsync(listResult.Value, ct);
                    listMap[tl.Id] = listResult.Value.Id.Value;
                    importedListIds.Add(listResult.Value.Id.Value);
                }

                listIndex++;
            }

            // Cards.
            int cardIndex = 0;
            foreach (var tc in tb.Cards ?? [])
            {
                if (!listMap.TryGetValue(tc.IdList, out var listId))
                {
                    cardIndex++;
                    continue;
                }

                var cardName = string.IsNullOrWhiteSpace(tc.Name) ? "Imported card" : tc.Name;
                var titleResult = CardTitle.Create(cardName);
                if (titleResult.IsFailure)
                {
                    cardIndex++;
                    continue;
                }

                var descResult2 = CardDescription.Create(tc.Desc ?? string.Empty);
                if (descResult2.IsFailure)
                {
                    cardIndex++;
                    continue;
                }

                DateTimeOffset? dueDate = null;
                if (tc.Due is { } dueString && DateTimeOffset.TryParse(dueString, out var parsed))
                {
                    dueDate = parsed;
                }

                var cardPosition = Position.From(cardIndex * 1024);
                var cardResult = Card.Create(
                    CardId.New(),
                    new BoardListId(listId),
                    titleResult.Value,
                    descResult2.Value,
                    cardPosition,
                    currentUser.Id.Value,
                    clock.UtcNow);

                if (cardResult.IsFailure)
                {
                    cardIndex++;
                    continue;
                }

                if (dueDate is { } dd)
                {
                    cardResult.Value.SetDueDate(dd, clock.UtcNow);
                }

                cardCount++;
                if (sampleCardNames.Count < MaxSampleCardNames)
                {
                    sampleCardNames.Add(cardName);
                }

                if (!previewOnly)
                {
                    await cards.AddAsync(cardResult.Value, ct);
                    importedCardIds.Add(cardResult.Value.Id.Value);
                }

                cardIndex++;
            }
        }

        if (!previewOnly)
        {
            await unitOfWork.SaveChangesAsync(ct);
        }

        var preview = new ImportPreview(
            BoardCount: boardCount,
            ListCount: listCount,
            CardCount: cardCount,
            LabelCount: labelCount,
            MemberCount: memberCount,
            SampleBoardNames: sampleBoardNames,
            SampleListNames: sampleListNames,
            SampleCardNames: sampleCardNames,
            WasApplied: !previewOnly);

        return Result.Success(new ImportResult(
            ImportedWorkspaceIds: [],
            ImportedBoardIds: importedBoardIds,
            ImportedListIds: importedListIds,
            ImportedCardIds: importedCardIds,
            ImportedLabelIds: importedLabelIds,
            Preview: preview));
    }

    private static string NormalizeTrelloColor(string? trelloColor)
    {
        if (string.IsNullOrWhiteSpace(trelloColor))
        {
            return Color.Palette.Gray.Value;
        }

        return trelloColor.ToLowerInvariant() switch
        {
            "yellow" => Color.Palette.Yellow.Value,
            "purple" => Color.Palette.Purple.Value,
            "blue" => Color.Palette.Blue.Value,
            "red" => Color.Palette.Red.Value,
            "green" => Color.Palette.Green.Value,
            "orange" => Color.Palette.Orange.Value,
            "black" => Color.Palette.Black.Value,
            "sky" => Color.Palette.Sky.Value,
            "lime" => Color.Palette.Lime.Value,
            "pink" => Color.Palette.Pink.Value,
            "gray" or "grey" => Color.Palette.Gray.Value,
            _ => trelloColor.StartsWith('#') ? trelloColor : Color.Palette.Gray.Value
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    // ── Trello JSON shape (loose, only the fields we need) ────

    private sealed class TrelloBoard
    {
        public string Name { get; set; } = string.Empty;
        public string? Desc { get; set; }
        public TrelloLabel[]? Labels { get; set; }
        public TrelloList[]? Lists { get; set; }
        public TrelloCard[]? Cards { get; set; }
        public TrelloMember[]? Members { get; set; }
    }

    private sealed class TrelloLabel
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Color { get; set; }
    }

    private sealed class TrelloList
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TrelloCard
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Desc { get; set; }
        public string IdList { get; set; } = string.Empty;
        public string? Due { get; set; }
        public string[]? IdLabels { get; set; }
        public string[]? IdMembers { get; set; }
    }

    private sealed class TrelloMember
    {
        public string Id { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
    }
}
