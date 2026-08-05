using System.Globalization;
using System.Text;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Calendar;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;

namespace Cardscape.Infrastructure.Calendar;

/// <summary>
/// Default <see cref="IIcalendarService"/> implementation. Emits
/// a minimal RFC 5545 <c>VCALENDAR</c> with one <c>VEVENT</c>
/// per card that has a <c>DueDate</c>:
/// <list type="bullet">
///   <item><c>UID</c>: <c>{cardId}@cardscape</c></item>
///   <item><c>SUMMARY</c>: the card's title</item>
///   <item><c>DESCRIPTION</c>: the card's description</item>
///   <item><c>DTSTART</c> / <c>DTEND</c>: the due date and the
///         following day (all-day events).</item>
///   <item><c>DTSTAMP</c>: now (UTC)</item>
/// </list>
/// </summary>
public sealed class IcsCalendarService(
    IBoardRepository boards,
    IBoardListRepository lists,
    ICardRepository cards,
    ICurrentUser currentUser) : IIcalendarService
{
    public async Task<Result<Stream>> RenderBoardAsync(Guid boardId, CancellationToken ct = default)
    {
        var board = await boards.GetByIdAsync(new BoardId(boardId), ct);
        if (board is null)
        {
            return Result.Failure<Stream>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        // Public boards are readable by anyone, including
        // anonymous (unauthenticated) callers — the endpoint
        // is mapped with .AllowAnonymous() and the public
        // contract is "any user with a link". Workspace and
        // Private boards require an authenticated member of
        // the right scope. The membership check below covers
        // both cases: a public board short-circuits the
        // `IsMember` check by the time we get here.
        if (board.Visibility == BoardVisibility.Public)
        {
            // Pass through — the board is publicly readable.
        }
        else
        {
            if (currentUser.Id is null)
            {
                return Result.Failure<Stream>(DomainError.Unauthenticated(
                    "auth.required", "Authentication is required."));
            }

            if (!board.IsMember(currentUser.Id.Value))
            {
                return Result.Failure<Stream>(DomainError.Forbidden(
                    "boards.forbidden", "You are not a member of this board."));
            }
        }

        IReadOnlyList<BoardList> boardLists = await lists.ListForBoardAsync(board.Id, includeArchived: false, ct);
        var allCards = new List<Card>();
        foreach (BoardList l in boardLists)
        {
            allCards.AddRange(await cards.ListForListAsync(l.Id, includeArchived: true, ct));
        }

        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Cardscape//Board " + boardId + "//EN");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:PUBLISH");
        sb.AppendLine($"X-WR-CALNAME:{Escape(board.Name.Value)}");

        DateTimeOffset stamp = DateTimeOffset.UtcNow;
        foreach (Card card in allCards.Where(c => c.DueDate.HasValue).OrderBy(c => c.DueDate))
        {
            DateTimeOffset due = card.DueDate!.Value;
            DateTimeOffset end = due.AddDays(1);
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:{card.Id.Value}@cardscape");
            sb.AppendLine($"DTSTAMP:{FormatIcs(stamp)}");
            sb.AppendLine($"DTSTART;VALUE=DATE:{FormatIcsDateOnly(due)}");
            sb.AppendLine($"DTEND;VALUE=DATE:{FormatIcsDateOnly(end)}");
            sb.AppendLine($"SUMMARY:{Escape(card.Title.Value)}");
            if (!string.IsNullOrWhiteSpace(card.Description.Value))
            {
                sb.AppendLine($"DESCRIPTION:{Escape(card.Description.Value)}");
            }
            sb.AppendLine("END:VEVENT");
        }

        sb.AppendLine("END:VCALENDAR");

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()))
        {
            Position = 0
        };
        return Result.Success<Stream>(stream);
    }

    private static string FormatIcs(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);

    private static string FormatIcsDateOnly(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    private static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case ';':
                    sb.Append("\\;");
                    break;
                case ',':
                    sb.Append("\\,");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    // Skip — RFC 5545 collapses \r\n to \n.
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return Fold(sb.ToString());
    }

    /// <summary>
    /// RFC 5545 §3.1 requires content lines (after unfolding
    /// escapes) to be no longer than 75 octets; longer lines
    /// are split with a CRLF + space. We measure UTF-8 octets,
    /// not chars, so multi-byte characters don't break the
    /// budget. Lines that would start with a space or tab get
    /// a CRLF only (no leading whitespace), per the spec.
    /// </summary>
    private static string Fold(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(text);
        if (bytes.Length <= 75)
        {
            return text;
        }

        var output = new StringBuilder(text.Length + 16);
        var currentLine = new List<byte>(75);
        foreach (byte b in bytes)
        {
            if (currentLine.Count >= 75)
            {
                output.Append(Encoding.UTF8.GetString(currentLine.ToArray()));
                currentLine.Clear();
                // Continuation line starts with a single space
                // so the unfolded result matches the original.
                output.Append("\r\n ");
            }
            currentLine.Add(b);
        }
        if (currentLine.Count > 0)
        {
            output.Append(Encoding.UTF8.GetString(currentLine.ToArray()));
        }
        return output.ToString();
    }
}
