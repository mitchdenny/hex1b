using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// Specifies how the backdrop is visually rendered.
/// </summary>
public enum BackdropStyle
{
    /// <summary>
    /// Transparent backdrop - base layer content shows through unchanged.
    /// Clicks are still captured.
    /// </summary>
    Transparent,
    
    /// <summary>
    /// Solid color backdrop - completely covers the base layer.
    /// </summary>
    Opaque
}
