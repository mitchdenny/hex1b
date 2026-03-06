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
}
