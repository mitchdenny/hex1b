using Hex1b.Documents;
using Hex1b.Theming;

namespace Hex1b.Widgets;

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
