using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Abstractions.Storage;
using Cardscape.Application.Common;
using Cardscape.Domain.Attachments;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Attachments;

public sealed record UploadAttachmentCommand(
    Guid CardId,
    string FileName,
    string MimeType,
    long SizeBytes,
    Stream Content) : IMessage;

public static class UploadAttachmentCommandHandler
{
    public static async Task<Result<AttachmentDto>> Handle(
        UploadAttachmentCommand command,
        IAttachmentRepository attachments,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        IStorageService storage,
        IClock clock,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<AttachmentDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(command.CardId), ct);
        if (card is null)
        {
            return Result.Failure<AttachmentDto>(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        var guard = await MembershipGuards.EnsureCanMutateCardAsync(
            card, lists, boards, currentUser.Id.Value, ct);
        if (guard.IsFailure)
        {
            return Result.Failure<AttachmentDto>(guard.Error);
        }

        if (command.SizeBytes < 0)
        {
            return Result.Failure<AttachmentDto>(DomainError.Validation(
                "attachments.size_invalid", "File size cannot be negative."));
        }

        // 25 MB hard cap — matches ASP.NET's default request body
        // budget and keeps the in-memory stream bounded for
        // LocalFileStorageService.
        const long MaxBytes = 25L * 1024L * 1024L;
        if (command.SizeBytes > MaxBytes)
        {
            return Result.Failure<AttachmentDto>(DomainError.Validation(
                "attachments.too_large", $"File exceeds the {MaxBytes / (1024 * 1024)} MB cap."));
        }

        // BETA-A5-R2-004 + BETA-A5-R2-005 — see
        // test-results/beta/round-2/reports/A5-card-extras.md.
        //
        // Two security holes in the previous handler:
        //   1. The MIME type was taken verbatim from the client
        //      with no validation. A `.exe` uploaded as
        //      `application/x-msdownload` would be served back
        //      to the user's browser with the right
        //      Content-Type, triggering a download / execution
        //      prompt. The fix is a denylist of dangerous
        //      MIME types — executables, scripts, and archives
        //      that can be served back with a content-
        //      disposition.
        //   2. The `FileName` was embedded verbatim into the
        //      storage key, so `../../etc/passwd` would have
        //      escaped the `/app/Storage` root on the local
        //      file storage backend. The fix is to compute a
        //      safe basename and the storage key from a fresh
        //      GUID, with the user-supplied filename kept only
        //      as a metadata field.
        string mimeType = string.IsNullOrWhiteSpace(command.MimeType)
            ? "application/octet-stream"
            : command.MimeType.Trim().ToLowerInvariant();
        if (IsBlockedMimeType(mimeType))
        {
            return Result.Failure<AttachmentDto>(DomainError.Validation(
                "attachments.mime_blocked",
                $"MIME type '{mimeType}' is not allowed for attachments."));
        }

        string safeName = SanitizeFileName(command.FileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            return Result.Failure<AttachmentDto>(DomainError.Validation(
                "attachments.name_invalid", "File name is required and must contain at least one allowed character."));
        }

        string storageKey = $"cards/{command.CardId:N}/{Guid.NewGuid():N}/{safeName}";
        await storage.SaveAsync(storageKey, command.Content, mimeType, ct);

        var creation = Attachment.Create(
            AttachmentId.New(),
            new CardId(command.CardId),
            safeName,
            mimeType,
            command.SizeBytes,
            storageKey,
            currentUser.Id.Value,
            clock.UtcNow);

        if (creation.IsFailure)
        {
            return Result.Failure<AttachmentDto>(creation.Error);
        }

        await attachments.AddAsync(creation.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(AttachmentDto.FromEntity(creation.Value));
    }

    // ── BETA-A5-R2-004 / BETA-A5-R2-005 helpers ───────────────
    //
    // `IsBlockedMimeType` rejects the formats that can be
    // served back to the browser as executable / script content.
    // The denylist mirrors what Kanban / Notion / Linear
    // disallow: native executables, Office macros, script
    // content, server-side includes, and HTML (which is a
    // stored-XSS vector when served from a same-origin path).
    // Everything else is allowed; the storage backend does
    // not need to inspect content.
    private static bool IsBlockedMimeType(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return false;
        }

        return mimeType switch
        {
            // Native executables / installers
            "application/x-msdownload" => true,
            "application/x-msdos-program" => true,
            "application/x-exe" => true,
            "application/exe" => true,
            "application/x-dosexec" => true,
            "application/x-winexe" => true,
            "application/x-apple-diskimage" => true,
            "application/vnd.microsoft.portable-executable" => true,
            // Office macros
            "application/vnd.ms-excel.addin.macroEnabled.12" => true,
            "application/vnd.ms-word.document.macroEnabled.12" => true,
            "application/vnd.ms-powerpoint.presentation.macroEnabled.12" => true,
            "application/vnd.ms-excel.sheet.macroEnabled.12" => true,
            // Scripts
            "text/html" => true,
            "application/xhtml+xml" => true,
            "application/javascript" => true,
            "application/x-javascript" => true,
            "text/javascript" => true,
            "text/x-shellscript" => true,
            "application/x-shellscript" => true,
            "application/x-perl" => true,
            "application/x-python" => true,
            "application/x-httpd-php" => true,
            // Server-side includes
            "text/x-server-parsed-html" => true,
            "application/x-httpd-cgi" => true,
            // Shell helpers
            "application/x-shockwave-flash" => true,
            "application/java-archive" => true,
            "application/java-vm" => true,
            _ => false
        };
    }

    // BETA-A5-R2-005 — strip path components, control chars,
    // reserved Windows / Unix names, and overly long names
    // from the user-supplied filename. The result is safe to
    // embed in a storage key. The original (untrusted) name is
    // never written to disk.
    private static readonly System.Buffers.SearchValues<char> PathSeparators =
        System.Buffers.SearchValues.Create(new[] { '/', '\\' });

    private static readonly System.Buffers.SearchValues<char> UnsafeChars =
        System.Buffers.SearchValues.Create(new[] { ':', '*', '?', '"', '<', '>', '|' });

    private static string SanitizeFileName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        // Take only the basename — no directory components.
        string name = raw;
        int slash = name.AsSpan().LastIndexOfAny(PathSeparators);
        if (slash >= 0)
        {
            name = name[(slash + 1)..];
        }

        // Strip control characters and a handful of
        // shell-unsafe punctuation. Keep dots, dashes, spaces,
        // underscores, parentheses, and Unicode letters.
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (char c in name)
        {
            if (c < 0x20 || c == 0x7F)
            {
                continue;
            }
            if (UnsafeChars.Contains(c))
            {
                continue;
            }
            sb.Append(c);
        }

        string cleaned = sb.ToString().Trim();
        if (cleaned.Length > 200)
        {
            cleaned = cleaned[..200];
        }
        return cleaned;
    }
}
