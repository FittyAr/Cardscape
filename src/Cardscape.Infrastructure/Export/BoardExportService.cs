using System.IO.Compression;
using System.Text.Json;
using Cardscape.Application.Abstractions.Export;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;

namespace Cardscape.Infrastructure.Export;

/// <summary>
/// Default <see cref="IExportService"/> implementation. Emits a
/// ZIP file with a <c>board.json</c> at the root (the structured
/// export of the board, lists, cards, and labels).
/// </summary>
public sealed class BoardExportService(
    IBoardRepository boards,
    IBoardListRepository lists,
    ICardRepository cards,
    ILabelRepository labels,
    ICurrentUser currentUser) : IExportService
{
    public async Task<Result<Stream>> ExportBoardAsync(Guid boardId, CancellationToken ct = default)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<Stream>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetByIdAsync(new BoardId(boardId), ct);
        if (board is null)
        {
            return Result.Failure<Stream>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<Stream>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        IReadOnlyList<BoardList> boardLists = await lists.ListForBoardAsync(board.Id, includeArchived: true, ct);
        var boardCards = new List<Card>();
        foreach (BoardList l in boardLists)
        {
            boardCards.AddRange(await cards.ListForListAsync(l.Id, includeArchived: true, ct));
        }
        IReadOnlyList<Label> boardLabels = await labels.ListForBoardAsync(board.Id, ct);

        var envelope = new
        {
            Schema = "cardscape.board.export.v1",
            ExportedAt = DateTimeOffset.UtcNow,
            Board = new
            {
                Id = board.Id.Value,
                WorkspaceId = board.WorkspaceId.Value,
                Name = board.Name.Value,
                Description = board.Description.Value,
                Visibility = board.Visibility.ToString(),
                IsArchived = board.IsArchived,
                CreatedAt = board.CreatedAt
            },
            Lists = boardLists.Select(l => new
            {
                Id = l.Id.Value,
                Name = l.Name.Value,
                Position = l.Position.Value,
                IsArchived = l.IsArchived
            }),
            Cards = boardCards.Select(c => new
            {
                Id = c.Id.Value,
                ListId = c.ListId.Value,
                Title = c.Title.Value,
                Description = c.Description.Value,
                Position = c.Position.Value,
                DueDate = c.DueDate,
                IsArchived = c.IsArchived,
                IsCompleted = c.IsCompleted
            }),
            Labels = boardLabels.Select(lb => new
            {
                Id = lb.Id.Value,
                Name = lb.Name.Value,
                Color = lb.Color.Value
            })
        };

        var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry jsonEntry = archive.CreateEntry("board.json", CompressionLevel.Optimal);
            await using var entryStream = jsonEntry.Open();
            await JsonSerializer.SerializeAsync(
                entryStream,
                envelope,
                JsonOptions,
                ct);
        }

        output.Position = 0;
        return Result.Success<Stream>(output);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}
