using Hex1b.Theming;

namespace Hex1b.Documents;

/// <summary>
/// Describes a floating UI element anchored to a document position in the editor.
/// Pushed by decoration providers via <see cref="IEditorSession.PushOverlay"/>.
/// </summary>
/// <param name="Id">Unique identifier for dismiss/update.</param>
/// <param name="AnchorPosition">Document position to anchor the overlay to (1-based line/column).</param>
/// <param name="Placement">Where to place the overlay relative to the anchor.</param>
/// <param name="Content">Lines of content to display in the overlay.</param>
/// <param name="DismissOnCursorMove">Whether to dismiss this overlay when the cursor moves.</param>
public record EditorOverlay(
    string Id,
    DocumentPosition AnchorPosition,
    OverlayPlacement Placement,
    IReadOnlyList<OverlayLine> Content,
    bool DismissOnCursorMove = true)
{
    /// <summary>Maximum width in columns. Null for auto-sizing.</summary>
    public int? MaxWidth { get; init; }

    /// <summary>Maximum height in rows. Null for auto-sizing.</summary>
    public int? MaxHeight { get; init; }

    /// <summary>Optional title shown in the overlay border.</summary>
    public string? Title { get; init; }
}

/// <summary>
/// A single line of styled text in an editor overlay.
/// </summary>
public record OverlayLine(string Text, Hex1bColor? Foreground = null, Hex1bColor? Background = null)
{
    /// <summary>
    /// Rich styled segments for this line. When set, these are used instead
    /// of Text/Foreground/Background for rendering.
    /// </summary>
    public IReadOnlyList<OverlaySegment>? Segments { get; init; }
}

/// <summary>
/// A styled text segment within an overlay line. Allows mixing colors
/// and styles within a single line.
/// </summary>
public record OverlaySegment(
    string Text,
    Hex1bColor? Foreground = null,
    Hex1bColor? Background = null)
{
    /// <summary>Whether this segment should be rendered in bold.</summary>
    public bool IsBold { get; init; }

    /// <summary>Whether this segment should be rendered in italic.</summary>
    public bool IsItalic { get; init; }
}

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
