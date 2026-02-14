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
    /// <param name="pendingNibble">For hex renderers, the first nibble of a partially-entered byte (null if none).</param>
    void Render(Hex1bRenderContext context, EditorState state, Rect viewport, int scrollOffset, int horizontalScrollOffset, bool isFocused, char? pendingNibble = null);

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

    /// <summary>
    /// Whether this renderer intercepts character input (e.g., hex byte editing).
    /// When <c>true</c>, <see cref="HandleCharInput"/> is called instead of
    /// the default text insertion for printable characters.
    /// </summary>
    bool HandlesCharInput => false;

    /// <summary>
    /// Processes a printable character for renderers that intercept input.
    /// Returns <c>true</c> if the input was consumed (including storing a
    /// partial value like a hex nibble). The <paramref name="pendingNibble"/>
    /// ref parameter holds per-editor-instance state managed by the caller.
    /// </summary>
    bool HandleCharInput(char c, EditorState state, ref char? pendingNibble, int viewportColumns) => false;

    /// <summary>
    /// Handles cursor navigation for this renderer.
    /// Returns <c>true</c> if the renderer handled the navigation (e.g., byte-level
    /// movement in hex mode), in which case the default character-level navigation
    /// is skipped.
    /// </summary>
    /// <param name="direction">The cursor direction.</param>
    /// <param name="state">The editor state.</param>
    /// <param name="extend">Whether to extend the selection.</param>
    /// <param name="viewportColumns">Available width in columns for responsive layout.</param>
    bool HandleNavigation(CursorDirection direction, EditorState state, bool extend, int viewportColumns) => false;
}
