using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b;

public class Hex1bRenderContext
{
    private readonly IHex1bAppTerminalWorkloadAdapter? _adapter;

    public Hex1bRenderContext(IHex1bAppTerminalWorkloadAdapter adapter, Hex1bTheme? theme = null)
    {
        _adapter = adapter;
        Theme = theme ?? Hex1bThemes.Default;
    }
    
    /// <summary>
    /// Protected constructor for derived classes that don't use an adapter.
    /// </summary>
    protected Hex1bRenderContext(Hex1bTheme? theme)
    {
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

    /// <summary>
    /// The ambient background color established by a parent container (e.g., a window
    /// or a highlighted table row). Leaf nodes like TextBlockNode
    /// should apply this background when rendering content so that characters written
    /// on top of a pre-painted region preserve the parent's background color.
    /// Container nodes that paint a background should set this before rendering their
    /// children and restore the previous value afterwards.
    /// </summary>
    public Hex1bColor AmbientBackground { get; set; } = Hex1bColor.Default;

    public virtual void EnterAlternateScreen() => _adapter?.EnterTuiMode();
    public virtual void ExitAlternateScreen() => _adapter?.ExitTuiMode();
    public virtual void Write(string text) => _adapter?.Write(text);
    public virtual void Clear() => _adapter?.Clear();
    public virtual void SetCursorPosition(int left, int top) => _adapter?.SetCursorPosition(left, top);
    public virtual int Width => _adapter?.Width ?? 0;
    public virtual int Height => _adapter?.Height ?? 0;
    
    /// <summary>
    /// Terminal capabilities (Sixel support, colors, etc.).
    /// </summary>
    public virtual TerminalCapabilities Capabilities => _adapter?.Capabilities ?? TerminalCapabilities.Modern;
    
    // Frame boundary tokens (APC format: ESC _ content ESC \)
    private const string FrameBeginSequence = "\x1b_HEX1BAPP:FRAME:BEGIN\x1b\\";
    private const string FrameEndSequence = "\x1b_HEX1BAPP:FRAME:END\x1b\\";
    
    // Synchronized Update Mode (DEC private mode 2026)
    // Tells compatible terminals to buffer output until the end sequence, then render atomically.
    // Terminals that don't support it safely ignore these sequences.
    private const string SyncUpdateBegin = "\x1b[?2026h";  // Begin synchronized update
    private const string SyncUpdateEnd = "\x1b[?2026l";    // End synchronized update
    
    /// <summary>
    /// Signals the beginning of a render frame to the presentation filter pipeline.
    /// When frame buffering is enabled, updates are accumulated until <see cref="EndFrame"/> is called.
    /// Also enables Synchronized Update Mode for terminals that support it (DEC 2026).
    /// </summary>
    public virtual void BeginFrame()
    {
        _adapter?.Write(SyncUpdateBegin);   // Tell terminal to start buffering
        _adapter?.Write(FrameBeginSequence); // Tell our filter pipeline
    }
    
    /// <summary>
    /// Signals the end of a render frame. The presentation filter pipeline will emit
    /// only the net changes between this frame and the previous committed frame.
    /// Also ends Synchronized Update Mode, triggering atomic render on supported terminals.
    /// </summary>
    public virtual void EndFrame()
    {
        _adapter?.Write(FrameEndSequence);  // Tell our filter pipeline
        _adapter?.Write(SyncUpdateEnd);      // Tell terminal to flush and render
    }
    
    /// <summary>
    /// Clears a rectangular region by writing spaces.
    /// Used for dirty region clearing to avoid full-screen flicker.
    /// Respects global background color from the theme if set.
    /// </summary>
    /// <param name="rect">The rectangle to clear.</param>
    public virtual void ClearRegion(Rect rect)
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
    public virtual void WriteClipped(int x, int y, string text)
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
    public virtual bool ShouldRenderAt(int x, int y)
    {
        return CurrentLayoutProvider?.ShouldRenderAt(x, y) ?? true;
    }
    
    /// <summary>
    /// Renders a child node. Container nodes should call this instead of child.Render(context)
    /// to enable the framework to apply optimizations like caching.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The base implementation simply calls child.Render(this). Derived contexts (like 
    /// SurfaceRenderContext) can override this to implement caching - checking if the child
    /// is dirty and using a cached surface if not.
    /// </para>
    /// <para>
    /// Containers that need custom rendering behavior can still call child.Render(context)
    /// directly, but won't benefit from caching optimizations.
    /// </para>
    /// </remarks>
    /// <param name="child">The child node to render.</param>
    public virtual void RenderChild(Hex1bNode child)
    {
        if (child == null) return;
        
        SetCursorPosition(child.Bounds.X, child.Bounds.Y);
        child.Render(this);
    }
}
