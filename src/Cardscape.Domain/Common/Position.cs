using Cardscape.Domain.Common;

namespace Cardscape.Domain.Common;

/// <summary>
/// A fractional position used to order cards inside a list and lists
/// inside a board. Inserting between two existing items averages
/// their positions; when the gap shrinks below
/// <see cref="Epsilon"/>, callers should rebalance the whole
/// container.
/// </summary>
public readonly record struct Position
{
    /// <summary>Smallest value such that a rebalance should happen.</summary>
    public const double Epsilon = 0.0001d;

    public double Value { get; }

    private Position(double value) => Value = value;

    /// <summary>Wraps an arbitrary double. Used for hydration from persistence.</summary>
    public static Position From(double value) => new(value);

    /// <summary>A position roughly in the middle of the canonical "0..1" range.</summary>
    public static Position Start() => new(1.0d);

    /// <summary>Position that places an item after the given previous position.</summary>
    public static Position After(Position previous) => new(previous.Value + 1.0d);

    /// <summary>Position that places an item before the given next position.</summary>
    public static Position Before(Position next) => new(next.Value / 2.0d);

    /// <summary>Position that sits between two existing positions.</summary>
    public static Position Between(Position previous, Position next) =>
        new((previous.Value + next.Value) / 2.0d);

    public override string ToString() => Value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
}
