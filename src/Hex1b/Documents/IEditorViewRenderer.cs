using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Renders a view of a document within an editor viewport.
/// Implementations define how document content is visualized (text, hex, etc.).
/// The EditorNode delegates rendering and hit testing to the active renderer.
/// </summary>
public interface IEditorViewRenderer
{
    /// <summary>
    /// Renders the document content into the given viewport area.
    /// </summary>
    /// <param name="context">The render context and theme.</param>
    /// <param name="state">The editor state (document, cursors, selections).</param>
    /// <param name="viewport">The screen area to render into.</param>
    /// <param name="scrollOffset">First visible line (1-based).</param>
    /// <param name="horizontalScrollOffset">First visible column (0-based).</param>
    /// <param name="isFocused">Whether the editor is currently focused.</param>
    void Render(Hex1bRenderContext context, EditorState state, Rect viewport, int scrollOffset, int horizontalScrollOffset, bool isFocused);

    /// <summary>
    /// Converts screen-local coordinates (relative to viewport origin) to a document offset.
    /// Returns null if the position doesn't map to a valid document location.
    /// </summary>
    /// <param name="localX">X coordinate relative to viewport left edge.</param>
    /// <param name="localY">Y coordinate relative to viewport top edge.</param>
    /// <param name="state">The editor state (document).</param>
    /// <param name="viewportColumns">Number of visible columns.</param>
    /// <param name="viewportLines">Number of visible lines.</param>
    /// <param name="scrollOffset">First visible line (1-based).</param>
    /// <param name="horizontalScrollOffset">First visible column (0-based).</param>
    DocumentOffset? HitTest(int localX, int localY, EditorState state, int viewportColumns, int viewportLines, int scrollOffset, int horizontalScrollOffset);

    /// <summary>
    /// Returns the total number of visual lines this renderer needs for the document.
    /// For text view, this equals the document line count.
    /// For hex view, this depends on bytes per row (which may adapt to viewport width).
    /// </summary>
    /// <param name="document">The document being rendered.</param>
    /// <param name="viewportColumns">Available width in columns. Responsive renderers use this to adapt layout.</param>
    int GetTotalLines(IHex1bDocument document, int viewportColumns);

    /// <summary>
    /// Returns the maximum visual width (in columns) needed for any line.
    /// Used for horizontal scroll calculations.
    /// For text view, this is the longest line length.
    /// For hex view, this is the computed row width (always ≤ viewport, so no horizontal scroll).
    /// </summary>
    int GetMaxLineWidth(IHex1bDocument document, int scrollOffset, int viewportLines, int viewportColumns);
}
