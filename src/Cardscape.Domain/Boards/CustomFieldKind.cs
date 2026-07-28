namespace Cardscape.Domain.Boards;

/// <summary>
/// The data kind of a <see cref="CustomFieldDefinition"/>. The
/// kind determines how <see cref="CustomFieldValue.ValueJson"/>
/// is parsed and validated; the database column is the same
/// <c>TEXT</c> for every kind so the schema is stable.
/// </summary>
public enum CustomFieldKind
{
    /// <summary>Free-form UTF-8 text. Max 4000 chars.</summary>
    Text = 0,

    /// <summary>64-bit floating-point number.</summary>
    Number = 1,

    /// <summary>ISO-8601 instant (UTC).</summary>
    Date = 2,

    /// <summary>
    /// One of the option ids declared in
    /// <see cref="CustomFieldDefinition.OptionsJson"/>. Use for
    /// fixed enumerations (e.g. Priority=Low|Med|High).
    /// </summary>
    Dropdown = 3,

    /// <summary>Boolean checkbox (true/false).</summary>
    Checkbox = 4
}
