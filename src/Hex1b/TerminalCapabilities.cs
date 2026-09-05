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
    /// <remarks>
    /// This is the coarse, back-compatible feature flag <c>SixelNode</c> reads to
    /// decide whether to advertise Sixel to the hosted workload. Adapters that
    /// participate in the richer discovery model from
    /// <see href="https://github.com/mitchdenny/hex1b/issues/455">#455</see> should set
    /// this consistently with <see cref="SixelSupport"/> (true whenever
    /// <see cref="SixelSupport"/> is not <see cref="Sixel.SixelPresentationSupport.None"/>),
    /// but the two properties are independently settable so existing callers that only
    /// set this flag keep working unchanged.
    /// </remarks>
    public bool SupportsSixel { get; init; }

    /// <summary>
    /// Describes how the effective presentation can render Sixel graphics: not at
    /// all, natively, via translation to another image protocol, or authoritatively
    /// in a headless model with no real display.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="Sixel.SixelPresentationSupport.None"/>, meaning "not
    /// probed" or "confirmed unsupported" — the two are indistinguishable from this
    /// property alone. Adapters that expose richer probe diagnostics (for example
    /// <see cref="ConsolePresentationAdapter"/>) let callers tell the two apart.
    /// </remarks>
    public Sixel.SixelPresentationSupport SixelSupport { get; init; }

    /// <summary>
    /// The protocol cell metrics (width/height in pixels, source, reliability) the
    /// presentation reported or that were derived from other authoritative geometry,
    /// or <see langword="null"/> when nothing has been discovered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is deliberately distinct from a documented default: <see langword="null"/>
    /// means "unknown," never "assumed 10x20." <see cref="Hex1bTerminal"/> only falls
    /// back to <see cref="Sixel.SixelCellMetrics.Unknown"/>-equivalent estimates (via
    /// <see cref="Sixel.SixelCellMetrics.FromCapabilities"/>) when this property is
    /// <see langword="null"/>, and does so at the point a placement is created —
    /// discovering a real value afterward never rewrites an existing placement's
    /// already-recorded metrics.
    /// </para>
    /// <para>
    /// See <see href="https://github.com/mitchdenny/hex1b/issues/455">#455</see> for
    /// the discovery precedence that populates this value.
    /// </para>
    /// </remarks>
    public Sixel.SixelCellMetrics? SixelCellMetrics { get; init; }
    
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
    /// Presentation supports Kitty Graphics Protocol (KGP) for inline image display.
    /// </summary>
    public bool SupportsKgp { get; init; }
    
    /// <summary>
    /// Whether the terminal supports retroactive variation selector width changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When true, VS15 (U+FE0E) arriving after a wide emoji retroactively shrinks
    /// it to 1 cell, and VS16 (U+FE0F) arriving after a narrow emoji widens it to 2 cells.
    /// The cursor position is adjusted accordingly.
    /// </para>
    /// <para>
    /// Modern terminals (Ghostty, Kitty, WezTerm, iTerm2) support this behavior.
    /// Legacy terminals (xterm, Alacritty, macOS Terminal) do not — VS15/VS16 only
    /// affect the glyph appearance, not the column width.
    /// </para>
    /// <para>
    /// Default is true to match modern terminal behavior. Set to false for legacy
    /// terminal compatibility.
    /// </para>
    /// </remarks>
    public bool SupportsRetroactiveVariationSelectors { get; init; } = true;
    
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
    /// Actual (possibly fractional) width of a terminal cell in pixels.
    /// </summary>
    /// <remarks>
    /// In browser-based terminals like xterm.js, cell width may be fractional due to
    /// font rendering. This is used for precise sixel sizing. If not set, falls back
    /// to <see cref="CellPixelWidth"/>.
    /// </remarks>
    public double ActualCellPixelWidth { get; init; }
    
    /// <summary>
    /// Gets the actual cell pixel width, using integer width as fallback.
    /// </summary>
    public double EffectiveCellPixelWidth => ActualCellPixelWidth > 0 ? ActualCellPixelWidth : CellPixelWidth;
    
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
    /// Presentation supports styled underlines (SGR 4:x — curly, dotted, dashed).
    /// Modern terminals like kitty, WezTerm, iTerm2 support this.
    /// When false, curly/dotted/dashed underlines fall back to single underline.
    /// </summary>
    public bool SupportsStyledUnderlines { get; init; }

    /// <summary>
    /// Presentation supports colored underlines (SGR 58).
    /// When false, underlines use the foreground color.
    /// </summary>
    public bool SupportsUnderlineColor { get; init; }

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
        SupportsStyledUnderlines = true,
        SupportsUnderlineColor = true,
        CellPixelWidth = 10,
        CellPixelHeight = 20
    };
    
    /// <summary>
    /// Minimal capabilities (dumb terminal).
    /// </summary>
    public static TerminalCapabilities Minimal => new()
    {
        SupportsRetroactiveVariationSelectors = false
    };
}
