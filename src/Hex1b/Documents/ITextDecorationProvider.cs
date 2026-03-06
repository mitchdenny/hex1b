namespace Hex1b.Documents;

/// <summary>
/// Provides text decorations (syntax highlighting, diagnostics, etc.) for an editor.
/// Implementations should be efficient — <see cref="GetDecorations"/> is called every render frame
/// for the visible viewport only. Cache aggressively and use <see cref="IHex1bDocument.Version"/>
/// to invalidate caches when the document changes.
/// </summary>
public interface ITextDecorationProvider
{
    /// <summary>
    /// Called when the provider is connected to an editor. Receives an <see cref="IEditorSession"/>
    /// that the provider can use to interact with the editor (e.g., push overlays, request re-renders).
    /// </summary>
    /// <param name="session">The editor session handle.</param>
    void Activate(IEditorSession session) { }

    /// <summary>
    /// Returns decorations for the specified range of visible lines.
    /// Only lines within the viewport are requested; providers should not compute decorations
    /// for the entire document.
    /// </summary>
    /// <param name="startLine">First visible line (1-based).</param>
    /// <param name="endLine">Last visible line (1-based).</param>
    /// <param name="document">The document being rendered.</param>
    /// <returns>Decoration spans covering the requested line range.</returns>
    IReadOnlyList<TextDecorationSpan> GetDecorations(
        int startLine,
        int endLine,
        IHex1bDocument document);

    /// <summary>
    /// Called when the provider is disconnected from an editor.
    /// Implementations should clean up any resources or subscriptions.
    /// </summary>
    void Deactivate() { }
}
