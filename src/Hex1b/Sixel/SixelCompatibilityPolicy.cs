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
