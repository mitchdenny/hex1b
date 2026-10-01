using System.Runtime.CompilerServices;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Controls how the Escape key behaves for a window.
/// </summary>
public enum WindowEscapeBehavior
{
    /// <summary>
    /// Escape closes the window (default).
    /// </summary>
    Close,

    /// <summary>
    /// Escape is ignored - window stays open.
    /// </summary>
    Ignore,

    /// <summary>
    /// Escape only closes non-modal windows.
    /// </summary>
    CloseNonModal
}
