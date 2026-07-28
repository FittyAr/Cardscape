using System.Text;

namespace Cardscape.Application.Activities.Queries;

/// <summary>
/// Opaque, URL-safe cursor for activity timeline pagination. The
/// wire format is <c>base64url("{unixMs}|{guid}")</c> — two pieces
/// of information that together identify a position in a
/// <c>(OccurredAt desc, Id desc)</c> ordering. <see cref="TryDecode"/>
/// is permissive: any malformed cursor is treated as "no cursor"
/// rather than an error, so a stale link just refreshes to the
/// first page.
/// </summary>
public static class ActivityCursor
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public static string Encode(DateTimeOffset occurredAt, Guid id)
    {
        long unixMs = occurredAt.ToUnixTimeMilliseconds();
        string raw = $"{unixMs}|{id:D}";
        return Base64UrlEncode(Encoding.UTF8.GetBytes(raw));
    }

    public static bool TryDecode(
        string? cursor, out DateTimeOffset occurredAt, out Guid id)
    {
        occurredAt = default;
        id = Guid.Empty;

        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            byte[] bytes = Base64UrlDecode(cursor);
            string raw = Encoding.UTF8.GetString(bytes);
            int sep = raw.IndexOf('|');
            if (sep <= 0 || sep == raw.Length - 1)
            {
                return false;
            }

            if (!long.TryParse(raw.AsSpan(0, sep), out long unixMs))
            {
                return false;
            }

            if (!Guid.TryParse(raw[(sep + 1)..], out id))
            {
                return false;
            }

            occurredAt = DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Clamp a user-supplied limit to the allowed range
    /// (<see cref="DefaultLimit"/> default, <see cref="MaxLimit"/>
    /// cap).</summary>
    public static int ClampLimit(int? limit)
    {
        if (limit is null || limit <= 0)
        {
            return DefaultLimit;
        }

        return limit > MaxLimit ? MaxLimit : limit.Value;
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private static byte[] Base64UrlDecode(string s)
    {
        string padded = s.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }
}
