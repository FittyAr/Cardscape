using System.Buffers;
using System.Collections.Frozen;
using System.Text;

namespace Cardscape.Application.Attachments;

internal static class AttachmentUploadPolicy
{
    private static readonly SearchValues<char> PathSeparators =
        SearchValues.Create(['/', '\\']);

    private static readonly SearchValues<char> UnsafeChars =
        SearchValues.Create([':', '*', '?', '"', '<', '>', '|']);

    private static readonly FrozenSet<string> BlockedMimeTypes = new[]
    {
        "application/x-msdownload",
        "application/x-msdos-program",
        "application/x-exe",
        "application/exe",
        "application/x-dosexec",
        "application/x-winexe",
        "application/x-apple-diskimage",
        "application/vnd.microsoft.portable-executable",
        "application/vnd.ms-excel.addin.macroenabled.12",
        "application/vnd.ms-word.document.macroenabled.12",
        "application/vnd.ms-powerpoint.presentation.macroenabled.12",
        "application/vnd.ms-excel.sheet.macroenabled.12",
        "text/html",
        "application/xhtml+xml",
        "application/javascript",
        "application/x-javascript",
        "text/javascript",
        "text/x-shellscript",
        "application/x-shellscript",
        "application/x-perl",
        "application/x-python",
        "application/x-httpd-php",
        "text/x-server-parsed-html",
        "application/x-httpd-cgi",
        "application/x-shockwave-flash",
        "application/java-archive",
        "application/java-vm"
    }.ToFrozenSet(StringComparer.Ordinal);

    public static string NormalizeMimeType(string? mimeType) =>
        string.IsNullOrWhiteSpace(mimeType)
            ? "application/octet-stream"
            : mimeType.Trim().ToLowerInvariant();

    public static bool IsBlockedMimeType(string mimeType) => BlockedMimeTypes.Contains(mimeType);

    public static string SanitizeFileName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        int slash = raw.AsSpan().LastIndexOfAny(PathSeparators);
        string name = slash >= 0 ? raw[(slash + 1)..] : raw;
        var cleaned = new StringBuilder(name.Length);
        foreach (char character in name)
        {
            if (character < 0x20 || character == 0x7F || UnsafeChars.Contains(character))
            {
                continue;
            }

            cleaned.Append(character);
        }

        string result = cleaned.ToString().Trim();
        return result.Length > 200 ? result[..200] : result;
    }
}
