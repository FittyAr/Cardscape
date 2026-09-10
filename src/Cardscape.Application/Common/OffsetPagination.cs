namespace Cardscape.Application.Common;

/// <summary>Canonical offset-pagination limits for collection queries.</summary>
public static class OffsetPagination
{
    public const int DefaultTake = 50;
    public const int MaxTake = 200;

    public static int NormalizeSkip(int? skip) => Math.Max(skip ?? 0, 0);

    public static int NormalizeTake(int? take) =>
        take is null or <= 0 ? DefaultTake : Math.Min(take.Value, MaxTake);
}
