using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Interface for nodes that provide layout/clipping services to descendants.
/// Child nodes can query their ancestor layout node to determine rendering behavior.
/// </summary>
public interface ILayoutProvider
{
    /// <summary>
    /// The effective clipping rectangle for this layout region.
    /// </summary>
    Rect ClipRect { get; }
    
    /// <summary>
    /// The clip mode for this layout region.
    /// </summary>
    ClipMode ClipMode { get; }
    
    /// <summary>
    /// Determines if a character at the given absolute position should be rendered.
    /// </summary>
    /// <param name="x">Absolute X position in terminal coordinates.</param>
    /// <param name="y">Absolute Y position in terminal coordinates.</param>
    /// <returns>True if the character should be rendered, false if it should be clipped.</returns>
    bool ShouldRenderAt(int x, int y);
    
    /// <summary>
    /// Clips a string that starts at the given position, returning only the visible portion.
    /// </summary>
    /// <param name="x">Starting absolute X position.</param>
    /// <param name="y">Absolute Y position.</param>
    /// <param name="text">The text to potentially clip.</param>
    /// <returns>
    /// A tuple containing:
    /// - adjustedX: The X position to start rendering (may be > x if left-clipped)
    /// - clippedText: The portion of text that should be rendered (may be empty)
    /// </returns>
    (int adjustedX, string clippedText) ClipString(int x, int y, string text);
}
