using Hex1b.Documents;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// Controls where an editor overlay is positioned relative to its anchor.
/// </summary>
public enum OverlayPlacement
{
    /// <summary>Below the anchor line (completions, diagnostics).</summary>
    Below,

    /// <summary>Above the anchor line (hover info when near bottom).</summary>
    Above,
}
