using System.Buffers;
using System.Text;

namespace Cardscape.Application.Attachments;

internal static class AttachmentUploadPolicy
{
    private static readonly SearchValues<char> PathSeparators =
        SearchValues.Create(['/', '\\']);

    private static readonly SearchValues<char> UnsafeChars =
        SearchValues.Create([':', '*', '?', '"', '<', '>', '|']);

    public static string NormalizeMimeType(string? mimeType) =>
        string.IsNullOrWhiteSpace(mimeType)
            ? "application/octet-stream"
            : mimeType.Trim().ToLowerInvariant();

    public static bool IsBlockedMimeType(string mimeType) => mimeType switch
    {
        "application/x-msdownload" => true,
        "application/x-msdos-program" => true,
        "application/x-exe" => true,
        "application/exe" => true,
        "application/x-dosexec" => true,
        "application/x-winexe" => true,
        "application/x-apple-diskimage" => true,
        "application/vnd.microsoft.portable-executable" => true,
        "application/vnd.ms-excel.addin.macroenabled.12" => true,
        "application/vnd.ms-word.document.macroenabled.12" => true,
        "application/vnd.ms-powerpoint.presentation.macroenabled.12" => true,
        "application/vnd.ms-excel.sheet.macroenabled.12" => true,
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
        "text/x-server-parsed-html" => true,
        "application/x-httpd-cgi" => true,
        "application/x-shockwave-flash" => true,
        "application/java-archive" => true,
        "application/java-vm" => true,
        _ => false
    };

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
