namespace Cardscape.Web.Shared;

/// <summary>Two-letter initials for the avatar circles (top bar,
/// comment list). "Ada Lovelace" → "AL", "ada" → "A".</summary>
public static class UserInitials
{
    public static string From(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        string[] parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1
            ? parts[0][..1].ToUpperInvariant()
            : string.Concat(parts[0][0], parts[^1][0]).ToUpperInvariant();
    }
}
