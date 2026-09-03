using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// Identifies where opaque (<c>P2</c> 0 or 2) Sixel backgrounds come from.
/// </summary>
internal enum SixelBackgroundSource
{
    /// <summary>
    /// Use the terminal background captured when the graphic was created.
    /// </summary>
    CapturedTerminalBackground,

    /// <summary>
    /// Use Sixel color register zero, the xterm/WezTerm compatibility behavior.
    /// </summary>
    PaletteRegisterZero,
}

/// <summary>
/// Identifies whether color registers are shared by the terminal or private per graphic.
/// </summary>
internal enum SixelPaletteScope
{
    TerminalPersistent,
    PrivatePerGraphic,
}

/// <summary>
/// Identifies how DECSDM (private mode 80) set/reset maps onto Sixel scrolling.
/// </summary>
/// <remarks>
/// The VT340 manual and hardware tests make <c>CSI ? 80 h</c> enable Sixel
/// scrolling. Current xterm documentation and implementation interpret the same
/// mode in the opposite direction. Hex1b selects the DEC interpretation and keeps
/// the inversion here rather than in a terminal-name check.
/// </remarks>
internal enum SixelDecsdmPolarity
{
    /// <summary>
    /// <c>CSI ? 80 h</c> enables Sixel scrolling; <c>CSI ? 80 l</c> disables it.
    /// </summary>
    Dec,

    /// <summary>
    /// <c>CSI ? 80 h</c> disables Sixel scrolling; <c>CSI ? 80 l</c> enables it.
    /// </summary>
    Xterm,
}

/// <summary>
/// Centralized, reviewable Sixel compatibility and resource policy.
/// </summary>
/// <remarks>
/// Every deviation from the DEC VT340 baseline and every allocation bound lives
/// here so it is testable and never expressed as a terminal-name check.
/// </remarks>
internal sealed record SixelCompatibilityPolicy
{
    /// <summary>
    /// Gets the selected Hex1b policy described by <c>docs/sixel-terminal-behavior.md</c>.
    /// </summary>
    public static SixelCompatibilityPolicy Default { get; } = new();

    /// <summary>
    /// Gets the number of addressable color registers. Registers outside this
    /// range are explicitly rejected rather than silently wrapped.
    /// </summary>
    public int ColorRegisterCount { get; init; } = 256;

    /// <summary>
    /// Gets the source used to fill unpainted pixels for opaque backgrounds.
    /// </summary>
    public SixelBackgroundSource BackgroundSource { get; init; } =
        SixelBackgroundSource.CapturedTerminalBackground;

    /// <summary>
    /// Gets the deterministic background used when the terminal background is unset.
    /// </summary>
    public Rgba32 DefaultBackground { get; init; } = new(0, 0, 0, 255);

    /// <summary>
    /// Gets the palette lifetime scope.
    /// </summary>
    public SixelPaletteScope PaletteScope { get; init; } = SixelPaletteScope.TerminalPersistent;

    /// <summary>
    /// Gets the DECSDM (private mode 80) polarity.
    /// </summary>
    public SixelDecsdmPolarity DecsdmPolarity { get; init; } = SixelDecsdmPolarity.Dec;

    /// <summary>
    /// Gets the Sixel scrolling state a reset restores.
    /// </summary>
    /// <remarks>
    /// DEC VT340 hardware reports and the manual identify scrolling as the normal
    /// behavior, so RIS and DECSTR restore it.
    /// </remarks>
    public bool DefaultSixelScrolling { get; init; } = true;

    /// <summary>
    /// Gets the xterm private mode 8452 state a reset restores.
    /// </summary>
    /// <remarks>
    /// The reset (default) behavior leaves the text cursor at its original column
    /// below the graphic. Setting the mode leaves it to the right of the graphic,
    /// which is confirmed only in xterm and RLogin.
    /// </remarks>
    public bool DefaultSixelCursorToRight { get; init; }

    /// <summary>
    /// Maps a DECSDM set/reset request onto the Sixel scrolling state.
    /// </summary>
    /// <param name="decsdmEnabled"><see langword="true"/> for <c>CSI ? 80 h</c>.</param>
    /// <returns><see langword="true"/> when Sixel scrolling should be enabled.</returns>
    public bool ResolveSixelScrolling(bool decsdmEnabled) =>
        DecsdmPolarity == SixelDecsdmPolarity.Xterm ? !decsdmEnabled : decsdmEnabled;

    /// <summary>
    /// Gets the maximum number of logical pixels a single graphic may materialize.
    /// </summary>
    public long MaximumRasterPixels { get; init; } = 16L * 1024 * 1024;

    /// <summary>
    /// Gets the maximum number of pixel writes performed while rasterizing.
    /// </summary>
    public long MaximumRasterOperations { get; init; } = 64L * 1024 * 1024;

    /// <summary>
    /// Gets the maximum number of sparse tiles retained for a single graphic.
    /// </summary>
    public int MaximumRasterTiles { get; init; } = 4 * 1024;

    /// <summary>
    /// Gets the edge length of a sparse raster tile.
    /// </summary>
    public int RasterTileSize { get; init; } = 64;
}
