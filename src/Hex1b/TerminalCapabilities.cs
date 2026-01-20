namespace Hex1b;

/// <summary>
/// Capabilities that inform how Hex1bTerminal optimizes output
/// and what features are available.
/// </summary>
public record TerminalCapabilities
{
    /// <summary>
    /// Presentation understands Hex1b delta protocol (not raw ANSI).
    /// Enables significant bandwidth optimization.
    /// </summary>
    public bool SupportsDeltaProtocol { get; init; }
    
    /// <summary>
    /// Presentation supports Sixel graphics.
    /// </summary>
    public bool SupportsSixel { get; init; }
    
    /// <summary>
    /// Presentation supports mouse tracking.
    /// </summary>
    public bool SupportsMouse { get; init; }
    
    /// <summary>
    /// Presentation supports true color (24-bit RGB).
    /// </summary>
    public bool SupportsTrueColor { get; init; }
    
    /// <summary>
    /// Presentation supports 256 colors.
    /// </summary>
    public bool Supports256Colors { get; init; }
    
    /// <summary>
    /// Presentation supports alternate screen buffer.
    /// </summary>
    public bool SupportsAlternateScreen { get; init; }
    
    /// <summary>
    /// Whether the presentation adapter handles alternate screen buffer save/restore natively.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When true, the upstream terminal (e.g., xterm, iTerm) handles the actual buffer
    /// save/restore when it receives escape sequences like <c>\x1b[?1049h/l</c>.
    /// Hex1bTerminal will still maintain its internal buffer for snapshots, but the
    /// primary responsibility for restoring content lies with the presentation layer.
    /// </para>
    /// <para>
    /// When false (default), Hex1bTerminal must fully emulate alternate screen behavior,
    /// saving and restoring its internal buffer when processing mode 1049.
    /// This is required for headless mode, embedded terminals, and WebSocket adapters.
    /// </para>
    /// </remarks>
    public bool HandlesAlternateScreenNatively { get; init; }
    
    /// <summary>
    /// Presentation supports bracketed paste mode.
    /// </summary>
    public bool SupportsBracketedPaste { get; init; }
    
    /// <summary>
    /// Width of a terminal character cell in pixels.
    /// Used for Sixel graphics scaling. Default is 10 pixels.
    /// </summary>
    /// <remarks>
    /// Most terminal emulators use fonts where cells are approximately 9-12 pixels wide.
    /// This can be queried from the terminal using CSI 16 t (XTWINOPS), but many terminals
    /// don't support this, so a reasonable default is provided.
    /// </remarks>
    public int CellPixelWidth { get; init; } = 10;
    
    /// <summary>
    /// Height of a terminal character cell in pixels.
    /// Used for Sixel graphics scaling. Default is 20 pixels.
    /// </summary>
    /// <remarks>
    /// Most terminal emulators use fonts where cells are approximately 16-24 pixels tall.
    /// This can be queried from the terminal using CSI 16 t (XTWINOPS), but many terminals
    /// don't support this, so a reasonable default is provided.
    /// </remarks>
    public int CellPixelHeight { get; init; } = 20;
    
    /// <summary>
    /// Default foreground color as RGB (0xRRGGBB).
    /// Used when responding to OSC 10 color queries.
    /// </summary>
    public int DefaultForeground { get; init; } = 0xFFFFFF;
    
    /// <summary>
    /// Default background color as RGB (0xRRGGBB).
    /// Used when responding to OSC 11 color queries.
    /// </summary>
    public int DefaultBackground { get; init; } = 0x000000;
    
    /// <summary>
    /// Default capabilities for a modern terminal.
    /// </summary>
    public static TerminalCapabilities Modern => new()
    {
        SupportsMouse = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
        SupportsAlternateScreen = true,
        HandlesAlternateScreenNatively = true,  // Real terminals handle buffer switching
        SupportsBracketedPaste = true,
        CellPixelWidth = 10,
        CellPixelHeight = 20
    };
    
    /// <summary>
    /// Minimal capabilities (dumb terminal).
    /// </summary>
    public static TerminalCapabilities Minimal => new();
}
