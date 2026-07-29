using Cardscape.Domain.Common;

namespace Cardscape.Domain.Cards;

/// <summary>Card aging mode. Controls the visual fade applied to stale cards on the board.</summary>
public enum CardAgingMode
{
    /// <summary>No aging. The card renders at full opacity regardless of activity.</summary>
    Disabled = 0,
    /// <summary>Card fades as time passes since the last activity.</summary>
    ByActivity = 1
}

/// <summary>
/// Per-card aging settings. Stored as a separate row keyed
/// by <see cref="CardId"/>; the absence of a row means
/// "use the board default" (which itself defaults to
/// disabled).
/// </summary>
public sealed class CardAgingSettings : Entity<CardId>
{
    public CardAgingMode Mode { get; private set; }
    public int StaleAfterDays { get; private set; }

    private CardAgingSettings() { }

    private CardAgingSettings(CardId cardId, CardAgingMode mode, int staleAfterDays, DateTimeOffset updatedAt)
    {
        Id = cardId;
        Mode = mode;
        StaleAfterDays = staleAfterDays;
        UpdatedAt = updatedAt;
    }

    public static Result<CardAgingSettings> Create(
        CardId cardId, CardAgingMode mode, int staleAfterDays, DateTimeOffset at)
    {
        if (staleAfterDays < 1 || staleAfterDays > 365)
        {
            return Result.Failure<CardAgingSettings>(DomainError.Validation(
                "card_aging.stale_after_days_out_of_range",
                "Stale-after-days must be between 1 and 365."));
        }
        CardAgingSettings settings = new(cardId, mode, staleAfterDays, at)
        {
            CreatedAt = at,
            UpdatedAt = at
        };
        return Result.Success(settings);
    }

    public Result Update(CardAgingMode mode, int staleAfterDays, DateTimeOffset at)
    {
        if (staleAfterDays < 1 || staleAfterDays > 365)
        {
            return Result.Failure(DomainError.Validation(
                "card_aging.stale_after_days_out_of_range",
                "Stale-after-days must be between 1 and 365."));
        }
        Mode = mode;
        StaleAfterDays = staleAfterDays;
        UpdatedAt = at;
        return Result.Success();
    }

    public bool IsStale(DateTimeOffset lastActivity, DateTimeOffset now) =>
        Mode == CardAgingMode.ByActivity
        && (now - lastActivity).TotalDays >= StaleAfterDays;
}
