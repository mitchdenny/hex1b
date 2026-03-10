using Hex1b.Widgets;

namespace Hex1b.Documents;

/// <summary>
/// Handle given to <see cref="ITextDecorationProvider"/> implementations for interacting
/// with the editor they are connected to. Enables providers to push overlays, access
/// editor state, and request re-renders.
/// </summary>
public interface IEditorSession
{
    /// <summary>The current editor state (document, cursors, selections).</summary>
    EditorState State { get; }

    /// <summary>Terminal capabilities for the current session.</summary>
    TerminalCapabilities Capabilities { get; }

    /// <summary>
    /// Requests the editor to re-render. Call this after asynchronous updates
    /// (e.g., when language server results arrive) to ensure decorations are refreshed.
    /// </summary>
    void Invalidate();

    /// <summary>
    /// Pushes a floating overlay anchored to a document position.
    /// If an overlay with the same ID already exists, it is replaced.
    /// </summary>
    void PushOverlay(EditorOverlay overlay);

    /// <summary>
    /// Dismisses an overlay by its unique ID.
    /// </summary>
    void DismissOverlay(string overlayId);

    /// <summary>
    /// Returns all currently active overlays.
    /// </summary>
    IReadOnlyList<EditorOverlay> ActiveOverlays { get; }

    /// <summary>
    /// Pushes inline hints (virtual text rendered inline without modifying the document).
    /// Replaces any previously pushed hints.
    /// </summary>
    void PushInlineHints(IReadOnlyList<InlineHint> hints);

    /// <summary>
    /// Clears all inline hints.
    /// </summary>
    void ClearInlineHints();

    /// <summary>
    /// Returns all currently active inline hints.
    /// </summary>
    IReadOnlyList<InlineHint> ActiveInlineHints { get; }

    /// <summary>
    /// Pushes range highlights (temporary background-colored document ranges).
    /// Replaces any previously pushed highlights.
    /// </summary>
    void PushRangeHighlights(IReadOnlyList<RangeHighlight> highlights);

    /// <summary>
    /// Clears all range highlights.
    /// </summary>
    void ClearRangeHighlights();

    /// <summary>
    /// Returns all currently active range highlights.
    /// </summary>
    IReadOnlyList<RangeHighlight> ActiveRangeHighlights { get; }

    /// <summary>
    /// Pushes gutter decorations (icons/markers in the editor margin).
    /// Replaces any previously pushed gutter decorations.
    /// </summary>
    void PushGutterDecorations(IReadOnlyList<GutterDecoration> decorations);

    /// <summary>
    /// Clears all gutter decorations.
    /// </summary>
    void ClearGutterDecorations();

    /// <summary>
    /// Returns all currently active gutter decorations.
    /// </summary>
    IReadOnlyList<GutterDecoration> ActiveGutterDecorations { get; }

    /// <summary>
    /// Sets folding regions (collapsible code regions).
    /// Replaces any previously set regions.
    /// </summary>
    void SetFoldingRegions(IReadOnlyList<FoldingRegion> regions);

    /// <summary>
    /// Returns all currently defined folding regions.
    /// </summary>
    IReadOnlyList<FoldingRegion> FoldingRegions { get; }

    /// <summary>
    /// Sets breadcrumb data (hierarchical document symbols for navigation).
    /// </summary>
    void SetBreadcrumbs(BreadcrumbData? data);

    /// <summary>
    /// Returns the current breadcrumb data, or null if none set.
    /// </summary>
    BreadcrumbData? Breadcrumbs { get; }

    /// <summary>
    /// Shows an action menu popup at the specified position.
    /// Returns the selected item's ID, or null if dismissed.
    /// </summary>
    Task<string?> ShowActionMenuAsync(ActionMenu menu);

    /// <summary>
    /// Shows a signature help panel.
    /// </summary>
    void ShowSignaturePanel(SignaturePanel panel);

    /// <summary>
    /// Dismisses the signature help panel.
    /// </summary>
    void DismissSignaturePanel();
}
