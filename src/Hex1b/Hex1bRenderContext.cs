using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Terminal;
using Hex1b.Theming;

namespace Hex1b;

public class Hex1bRenderContext
{
    private readonly IHex1bAppTerminalWorkloadAdapter _adapter;

    public Hex1bRenderContext(IHex1bAppTerminalWorkloadAdapter adapter, Hex1bTheme? theme = null)
    {
        _adapter = adapter;
        Theme = theme ?? Hex1bThemes.Default;
    }

    public Hex1bTheme Theme { get; set; }
    
    /// <summary>
    /// The current mouse X position (0-based column), or -1 if mouse is not tracked.
    /// </summary>
    public int MouseX { get; set; } = -1;
    
    /// <summary>
    /// The current mouse Y position (0-based row), or -1 if mouse is not tracked.
    /// </summary>
    public int MouseY { get; set; } = -1;
    
    /// <summary>
    /// The current layout provider in scope. Child nodes can use this to query
    /// whether characters should be rendered (for clipping support).
    /// Layout providers should consult their ParentLayoutProvider to ensure
    /// proper nested clipping.
    /// </summary>
    public ILayoutProvider? CurrentLayoutProvider { get; set; }

    public void EnterAlternateScreen() => _adapter.EnterTuiMode();
    public void ExitAlternateScreen() => _adapter.ExitTuiMode();
    public void Write(string text) => _adapter.Write(text);
    public void Clear() => _adapter.Clear();
    public void SetCursorPosition(int left, int top) => _adapter.SetCursorPosition(left, top);
    public int Width => _adapter.Width;
    public int Height => _adapter.Height;
    
    /// <summary>
    /// Terminal capabilities (Sixel support, colors, etc.).
    /// </summary>
    public TerminalCapabilities Capabilities => _adapter.Capabilities;
    
    /// <summary>
    /// Clears a rectangular region by writing spaces.
    /// Used for dirty region clearing to avoid full-screen flicker.
    /// Respects global background color from the theme if set.
    /// </summary>
    /// <param name="rect">The rectangle to clear.</param>
    public void ClearRegion(Rect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        
        // Clamp to terminal bounds
        var startX = Math.Max(0, rect.X);
        var startY = Math.Max(0, rect.Y);
        var endX = Math.Min(Width, rect.X + rect.Width);
        var endY = Math.Min(Height, rect.Y + rect.Height);
        
        if (startX >= endX || startY >= endY) return;
        
        var width = endX - startX;
        var spaces = new string(' ', width);
        
        // Use global background color from theme if set, otherwise reset to default
        var bg = Theme.GetGlobalBackground();
        if (!bg.IsDefault)
        {
            var bgCode = bg.ToBackgroundAnsi();
            Write(bgCode);
        }
        else
        {
            Write("\x1b[0m");
        }
        
        for (var y = startY; y < endY; y++)
        {
            SetCursorPosition(startX, y);
            Write(spaces);
        }
        
        // Reset after clearing
        Write("\x1b[0m");
    }
    
    /// <summary>
    /// Writes text at the specified position, respecting the current layout provider's clipping.
    /// If no layout provider is active, the text is written as-is.
    /// </summary>
    /// <param name="x">The X position to start writing.</param>
    /// <param name="y">The Y position to write at.</param>
    /// <param name="text">The text to write.</param>
    public void WriteClipped(int x, int y, string text)
    {
        if (CurrentLayoutProvider == null)
        {
            // No layout provider - write directly
            SetCursorPosition(x, y);
            Write(text);
            return;
        }
        
        var (adjustedX, clippedText) = CurrentLayoutProvider.ClipString(x, y, text);
        if (clippedText.Length > 0)
        {
            SetCursorPosition(adjustedX, y);
            Write(clippedText);
        }
    }
    
    /// <summary>
    /// Checks if a position should be rendered based on the current layout provider.
    /// If no layout provider is active, returns true.
    /// </summary>
    public bool ShouldRenderAt(int x, int y)
    {
        return CurrentLayoutProvider?.ShouldRenderAt(x, y) ?? true;
    }
}
