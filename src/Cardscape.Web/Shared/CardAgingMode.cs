using System.Text.Json;

namespace Cardscape.Web.Shared;

/// <summary>
/// Mirrors the domain's card-aging mode (the Web project only references
/// the contracts). The value round-trips through the CardAging board
/// extension's config JSON: <c>{"mode":"ByActivity"}</c>.
/// </summary>
public enum CardAgingMode
{
    Disabled = 0,
    ByActivity = 1,
}

public static class CardAgingModeExtensions
{
    extension(CardAgingMode mode)
    {
        /// <summary>The mode stored in a CardAging config; anything unreadable means disabled.</summary>
        public static CardAgingMode FromConfigJson(string? configJson)
        {
            if (string.IsNullOrWhiteSpace(configJson))
            {
                return CardAgingMode.Disabled;
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(configJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("mode", out JsonElement modeEl)
                    && modeEl.ValueKind == JsonValueKind.String
                    && Enum.TryParse(modeEl.GetString(), ignoreCase: true, out CardAgingMode parsed)
                    && Enum.IsDefined(parsed))
                {
                    return parsed;
                }
            }
            catch (JsonException)
            {
                // Fall through to the default below.
            }

            return CardAgingMode.Disabled;
        }

        public string ToConfigJson() => $$"""{"mode":"{{mode}}"}""";

        /// <summary>
        /// Linear opacity: cards stay at full opacity until the staleness
        /// window, then fade toward 0.6 (the "stale but still legible"
        /// floor) over the same window. ByActivity: window = 14 days since
        /// the last update.
        /// </summary>
        public double CardOpacity(CardSummaryDto card, DateTimeOffset now)
        {
            if (mode == CardAgingMode.Disabled)
            {
                return 1.0;
            }

            const double fadeFloor = 0.6;
            const double windowDays = 14.0;
            double daysSince = Math.Max(0, (now - card.UpdatedAt).TotalDays);
            double fade = Math.Min(1.0, daysSince / windowDays);
            return fadeFloor + (1.0 - fadeFloor) * (1.0 - fade);
        }
    }
}
