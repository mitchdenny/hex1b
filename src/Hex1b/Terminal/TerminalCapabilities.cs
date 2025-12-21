namespace Hex1b.Terminal;

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
    /// Presentation supports bracketed paste mode.
    /// </summary>
    public bool SupportsBracketedPaste { get; init; }
    
    /// <summary>
    /// Default capabilities for a modern terminal.
    /// </summary>
    public static TerminalCapabilities Modern => new()
    {
        SupportsMouse = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
        SupportsAlternateScreen = true,
        SupportsBracketedPaste = true
    };
    
    /// <summary>
    /// Minimal capabilities (dumb terminal).
    /// </summary>
    public static TerminalCapabilities Minimal => new();
}
