using System.Text.RegularExpressions;
using Cardscape.Domain.Common;

namespace Cardscape.Domain.Common;

/// <summary>
/// An sRGB color in the <c>#RRGGBB</c> format. Used by labels, board
/// backgrounds, and any other place where a CSS-style color is
/// required.
/// </summary>
public sealed record Color : IValueObject
{
    private static readonly Regex HexRegex = new(
        @"^#[0-9a-fA-F]{6}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string Value { get; }

    private Color(string value) => Value = value;

    public static Result<Color> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result.Failure<Color>(DomainError.Validation(
                "common.color.required",
                "Color is required."));
        }

        if (!HexRegex.IsMatch(input))
        {
            return Result.Failure<Color>(DomainError.Validation(
                "common.color.invalid",
                "Color must be a 6-digit hex value in the #RRGGBB format."));
        }

        return Result.Success(new Color(input));
    }

    /// <summary>Common Kanban-style palette, indexed by name.</summary>
    public static class Palette
    {
        public static readonly Color Yellow = Create("#f2c600").Value;
        public static readonly Color Purple = Create("#a97bcf").Value;
        public static readonly Color Blue = Create("#0079bf").Value;
        public static readonly Color Red = Create("#eb5a46").Value;
        public static readonly Color Green = Create("#61bd4f").Value;
        public static readonly Color Orange = Create("#ff9f1f").Value;
        public static readonly Color Black = Create("#344563").Value;
        public static readonly Color Sky = Create("#00c2e0").Value;
        public static readonly Color Lime = Create("#51e898").Value;
        public static readonly Color Pink = Create("#ff78cb").Value;
        public static readonly Color Gray = Create("#b3bac5").Value;

        // BETA-A4-009 — see
        // test-results/beta/round-2/reports/A4-cards-lists.md.
        // The cover API accepts a colour name
        // ("yellow", "blue", …) and looks it up here; an
        // unknown name surfaces as 400 instead of being
        // silently dropped on the floor. Case-insensitive
        // — the palette names are the same Kanban uses.
        private static readonly System.Collections.Generic.Dictionary<string, Color> ByNameLookup =
            new(System.StringComparer.OrdinalIgnoreCase)
            {
                ["yellow"] = Yellow,
                ["purple"] = Purple,
                ["blue"] = Blue,
                ["red"] = Red,
                ["green"] = Green,
                ["orange"] = Orange,
                ["black"] = Black,
                ["sky"] = Sky,
                ["lime"] = Lime,
                ["pink"] = Pink,
                ["gray"] = Gray,
                ["grey"] = Gray,
            };

        public static Color? ByName(string? name) =>
            !string.IsNullOrWhiteSpace(name) && ByNameLookup.TryGetValue(name, out Color? c)
                ? c
                : null;
    }
}
