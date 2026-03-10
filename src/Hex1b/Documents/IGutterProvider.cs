using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b.Documents;

/// <summary>
/// Provides a gutter column rendered to the left of the editor content area.
/// Multiple providers can be composed — they render left-to-right in registration order.
/// </summary>
public interface IGutterProvider
{
    /// <summary>
    /// Returns the width in columns this provider needs.
    /// Called during layout to compute the total gutter area.
    /// </summary>
    /// <param name="document">The document being edited.</param>
    int GetWidth(IHex1bDocument document);

    /// <summary>
    /// Renders the gutter content for a single visible line.
    /// </summary>
    /// <param name="context">The render context for writing output.</param>
    /// <param name="theme">The active theme.</param>
    /// <param name="screenX">The screen X coordinate for this provider's column.</param>
    /// <param name="screenY">The screen Y coordinate for this line.</param>
    /// <param name="docLine">The 1-based document line number, or 0 if past end of document.</param>
    /// <param name="width">The width allocated to this provider (from <see cref="GetWidth"/>).</param>
    void RenderLine(Hex1bRenderContext context, Hex1bTheme theme, int screenX, int screenY, int docLine, int width);

    /// <summary>
    /// Handles a click in this provider's gutter column.
    /// Returns true if the click was consumed.
    /// </summary>
    /// <param name="docLine">The 1-based document line that was clicked.</param>
    bool HandleClick(int docLine) => false;
}
