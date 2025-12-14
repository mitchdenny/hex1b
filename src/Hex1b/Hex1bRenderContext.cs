using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b;

public class Hex1bRenderContext
{
    private readonly IHex1bTerminalOutput _output;

    public Hex1bRenderContext(IHex1bTerminalOutput output, Hex1bTheme? theme = null)
    {
        _output = output;
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
    /// </summary>
    public ILayoutProvider? CurrentLayoutProvider { get; set; }
    
    /// <summary>
    /// The inherited foreground color from parent containers (e.g., Panel).
    /// Nodes should use this when rendering text if they don't have their own color.
    /// </summary>
    public Hex1bColor InheritedForeground { get; set; } = Hex1bColor.Default;
    
    /// <summary>
    /// The inherited background color from parent containers (e.g., Panel).
    /// Nodes should use this when rendering to maintain visual continuity.
    /// </summary>
    public Hex1bColor InheritedBackground { get; set; } = Hex1bColor.Default;

    /// <summary>
    /// Gets the ANSI codes to apply inherited colors, or empty string if default.
    /// </summary>
    public string GetInheritedColorCodes()
    {
        var result = "";
        if (!InheritedForeground.IsDefault)
            result += InheritedForeground.ToForegroundAnsi();
        if (!InheritedBackground.IsDefault)
            result += InheritedBackground.ToBackgroundAnsi();
        return result;
    }
    
    /// <summary>
    /// Gets the ANSI codes to reset colors back to inherited values (or default if none).
    /// Use this after applying temporary color changes.
    /// </summary>
    public string GetResetToInheritedCodes()
    {
        if (InheritedForeground.IsDefault && InheritedBackground.IsDefault)
            return "\x1b[0m";
        
        var result = "\x1b[0m"; // Reset all first
        if (!InheritedForeground.IsDefault)
            result += InheritedForeground.ToForegroundAnsi();
        if (!InheritedBackground.IsDefault)
            result += InheritedBackground.ToBackgroundAnsi();
        return result;
    }

    public void EnterAlternateScreen() => _output.EnterAlternateScreen();
    public void ExitAlternateScreen() => _output.ExitAlternateScreen();
    public void Write(string text) => _output.Write(text);
    public void Clear() => _output.Clear();
    public void SetCursorPosition(int left, int top) => _output.SetCursorPosition(left, top);
    public int Width => _output.Width;
    public int Height => _output.Height;
    
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
