namespace Cardscape.Domain.Common;

/// <summary>
/// Marker interface for value objects. Records already provide
/// value-based equality, so this interface is purely a way to
/// express intent and to constrain generic parameters (e.g. when
/// projecting from a query).
/// </summary>
public interface IValueObject
{
}
