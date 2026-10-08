namespace Cardscape.Web.Shared;

/// <summary>How much room a <see cref="LoadingState"/> takes.</summary>
public enum LoadingSize
{
    /// <summary>Fills the content area; used while a route loads.</summary>
    Page,

    /// <summary>A padded block inside a card, panel or dialog.</summary>
    Section,

    /// <summary>Glyph and text on a single line.</summary>
    Inline,
}
