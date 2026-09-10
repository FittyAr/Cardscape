using System.Text.Json;
using Cardscape.Domain.Common;

namespace Cardscape.Infrastructure.Import;

internal static class KanbanArchiveReader
{
    internal const int MaxBytes = 10 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    internal static async Task<Result<KanbanBoard[]>> ReadAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.CanSeek && source.Length - source.Position > MaxBytes)
        {
            return PayloadTooLarge();
        }

        await using var buffer = new MemoryStream();
        byte[] chunk = new byte[16 * 1024];
        while (true)
        {
            int count = await source.ReadAsync(chunk, cancellationToken);
            if (count == 0)
            {
                break;
            }

            if (buffer.Length + count > MaxBytes)
            {
                return PayloadTooLarge();
            }

            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
        }

        buffer.Position = 0;
        KanbanBoard[]? boards;
        try
        {
            boards = await JsonSerializer.DeserializeAsync<KanbanBoard[]>(
                buffer,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return Result.Failure<KanbanBoard[]>(DomainError.Validation(
                "imports.invalid_json",
                "Kanban export is not valid JSON."));
        }

        return boards is { Length: > 0 }
            ? Result.Success(boards)
            : Result.Failure<KanbanBoard[]>(DomainError.Validation(
                "imports.empty_archive",
                "Kanban export contains no boards."));
    }

    private static Result<KanbanBoard[]> PayloadTooLarge() =>
        Result.Failure<KanbanBoard[]>(DomainError.Validation(
            "imports.payload_too_large",
            $"Kanban boards.json exceeds the {MaxBytes}-byte cap."));
}

internal sealed class KanbanBoard
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public KanbanLabel[]? Labels { get; set; }
    public KanbanList[]? Lists { get; set; }
    public KanbanCard[]? Cards { get; set; }
    public KanbanMember[]? Members { get; set; }
}

internal sealed class KanbanLabel
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Color { get; set; }
}

internal sealed class KanbanList
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

internal sealed class KanbanCard
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ListId { get; set; } = string.Empty;
    public string? DueDate { get; set; }
    public string[]? LabelIds { get; set; }
    public string[]? MemberIds { get; set; }
}

internal sealed class KanbanMember
{
    public string Id { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
}
