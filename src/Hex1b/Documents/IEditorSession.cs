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
}
