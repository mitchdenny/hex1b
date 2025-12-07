using System.Net.WebSockets;

namespace Gallery;

/// <summary>
/// Represents a gallery exhibit that can be displayed in the terminal gallery.
/// </summary>
public interface IGalleryExhibit
{
    /// <summary>
    /// Unique identifier for this exhibit (used in URLs).
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display title for this exhibit.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Brief description of what this exhibit demonstrates.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Source code snippet to display for this exhibit.
    /// </summary>
    string SourceCode { get; }

    /// <summary>
    /// Handle a WebSocket terminal session for this exhibit.
    /// </summary>
    Task HandleSessionAsync(WebSocket webSocket, TerminalSession session, CancellationToken cancellationToken);
}

/// <summary>
/// Represents a terminal session with size information.
/// </summary>
public class TerminalSession
{
    public int Cols { get; set; } = 80;
    public int Rows { get; set; } = 24;
    
    public event Action<int, int>? OnResize;

    public void Resize(int cols, int rows)
    {
        Cols = cols;
        Rows = rows;
        OnResize?.Invoke(cols, rows);
    }
}
