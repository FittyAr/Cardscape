using Microsoft.AspNetCore.Components;

namespace Cardscape.Web.Shared;

/// <summary>
/// A label/value pair for the <see cref="MetadataList"/> component.
/// The <see cref="Value"/> is a <see cref="RenderFragment"/> so callers
/// can render plain text, Radzen badges, action buttons, etc.
/// </summary>
public sealed record MetadataListItem(string Label, RenderFragment Value)
{
    public static MetadataListItem Text(string label, string value) => new(label, builder =>
    {
        builder.OpenElement(0, "span");
        builder.AddContent(1, value);
        builder.CloseElement();
    });
}
