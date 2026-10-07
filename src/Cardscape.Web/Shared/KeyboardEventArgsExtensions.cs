using Microsoft.AspNetCore.Components.Web;

namespace Cardscape.Web.Shared;

/// <summary>Key checks the board, card and composer handlers share.</summary>
public static class KeyboardEventArgsExtensions
{
    extension(KeyboardEventArgs e)
    {
        public bool IsEnter => e.Key == "Enter";

        public bool IsEscape => e.Key == "Escape";

        /// <summary>Enter or Space: the keys that activate a button-like element.</summary>
        public bool IsActivation => e.Key is "Enter" or " ";
    }
}
