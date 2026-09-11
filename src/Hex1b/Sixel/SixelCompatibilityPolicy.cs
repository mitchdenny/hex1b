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
/// Identifies how a reference implementation applies Sixel pixel-aspect metadata.
/// </summary>
internal enum SixelAspectBehavior
{
    /// <summary>Apply the DEC macro and DECGRA pixel aspect ratio.</summary>
    Dec,

    /// <summary>Render logical Sixel pixels as square pixels.</summary>
    SquarePixels,
}

/// <summary>
/// Identifies whether a color-register definition also selects that register.
/// </summary>
internal enum SixelColorDefinitionBehavior
{
    /// <summary>Defining a register also selects it, as specified by DEC.</summary>
    DefineAndSelect,

    /// <summary>Defining a register leaves the current selection unchanged.</summary>
    DefineOnly,
}

/// <summary>
/// Identifies how a zero DECGRI repeat count is interpreted.
/// </summary>
internal enum SixelZeroRepeatBehavior
{
    /// <summary>Treat zero as the DEC default of one repetition.</summary>
    RepeatOnce,

    /// <summary>Treat zero as no repetitions.</summary>
    RepeatZeroTimes,
}

/// <summary>
/// Identifies how 0-100 RGB components are converted to 8-bit values.
/// </summary>
internal enum SixelRgbQuantization
{
    /// <summary>Round to the nearest 8-bit component.</summary>
    Nearest,

    /// <summary>Truncate the scaled component toward zero.</summary>
    Truncate,
}

/// <summary>
/// Identifies the initial Sixel color-register map used by a reference profile.
/// </summary>
internal enum SixelInitialPalette
{
    /// <summary>Use the DEC VT340 palette with Hex1b's documented 256-color extension.</summary>
    DecVt340Extended,

    /// <summary>Use the palette initialized by WezTerm 20240203.</summary>
    WezTerm20240203,
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
    /// <summary>Gets the DEC VT340 baseline used by Hex1b.</summary>
    public static SixelCompatibilityPolicy DecVt340 { get; } = new();

    /// <summary>
    /// Gets the selected Hex1b/DEC VT340 policy described by
    /// <c>docs/sixel-terminal-behavior.md</c>.
    /// </summary>
    public static SixelCompatibilityPolicy Default { get; } = DecVt340;

    /// <summary>
    /// Gets the source-derived xterm patch 411 profile used by the differential
    /// conformance corpus.
    /// </summary>
    public static SixelCompatibilityPolicy Xterm411 { get; } = new()
    {
        BackgroundSource = SixelBackgroundSource.PaletteRegisterZero,
        DecsdmPolarity = SixelDecsdmPolarity.Xterm,
        AspectBehavior = SixelAspectBehavior.SquarePixels,
        InitialColorRegister = 3,
    };

    /// <summary>
    /// Gets the source-derived WezTerm 20240203-110809-5046fc22 profile used by
    /// the differential conformance corpus.
    /// </summary>
    public static SixelCompatibilityPolicy WezTerm20240203 { get; } = new()
    {
        BackgroundSource = SixelBackgroundSource.PaletteRegisterZero,
        DecsdmPolarity = SixelDecsdmPolarity.Xterm,
        AspectBehavior = SixelAspectBehavior.SquarePixels,
        ColorDefinitionBehavior = SixelColorDefinitionBehavior.DefineOnly,
        ZeroRepeatBehavior = SixelZeroRepeatBehavior.RepeatZeroTimes,
        RgbQuantization = SixelRgbQuantization.Truncate,
        InitialPalette = SixelInitialPalette.WezTerm20240203,
        InitialDrawingColor = new Rgba32(0, 255, 0, 255),
        RejectZeroExtentGraphics = true,
    };

    /// <summary>
    /// Gets the maximum number of DCS content bytes retained for tokenization,
    /// snapshots, and replay. Framing and geometry observation continue after
    /// this limit so the parser can recover at CAN, SUB, or ST.
    /// </summary>
    public int MaximumRetainedDcsBytes { get; init; } = 1024 * 1024;

    /// <summary>Gets the maximum number of parameters accepted in a DCS introducer.</summary>
    public int MaximumDcsHeaderParameters { get; init; } = 16;

    /// <summary>Gets the maximum numeric value accepted by DCS and Sixel parameters.</summary>
    public int MaximumNumericValue { get; init; } = 999_999_999;

    /// <summary>Gets the maximum number of raster commands retained for one graphic.</summary>
    public int MaximumRetainedCommands { get; init; } = 65_536;

    /// <summary>Gets the maximum number of palette mutations retained for one graphic.</summary>
    public int MaximumPaletteMutations { get; init; } = 4_096;

    /// <summary>Gets the maximum number of parser diagnostics retained for one graphic.</summary>
    public int MaximumDiagnostics { get; init; } = 64;

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
    /// Gets how pixel-aspect metadata affects rendered geometry.
    /// </summary>
    public SixelAspectBehavior AspectBehavior { get; init; } = SixelAspectBehavior.Dec;

    /// <summary>
    /// Gets whether a color definition also selects the defined register.
    /// </summary>
    public SixelColorDefinitionBehavior ColorDefinitionBehavior { get; init; } =
        SixelColorDefinitionBehavior.DefineAndSelect;

    /// <summary>
    /// Gets how a zero DECGRI repeat count is interpreted.
    /// </summary>
    public SixelZeroRepeatBehavior ZeroRepeatBehavior { get; init; } =
        SixelZeroRepeatBehavior.RepeatOnce;

    /// <summary>
    /// Gets how RGB percentage components are quantized to 8-bit values.
    /// </summary>
    public SixelRgbQuantization RgbQuantization { get; init; } =
        SixelRgbQuantization.Nearest;

    /// <summary>
    /// Gets the initial color-register map restored when the terminal is created
    /// and when RIS resets its persistent Sixel palette.
    /// </summary>
    public SixelInitialPalette InitialPalette { get; init; } =
        SixelInitialPalette.DecVt340Extended;

    /// <summary>
    /// Gets the register selected before the first DECGCI command.
    /// </summary>
    public int InitialColorRegister { get; init; }

    /// <summary>
    /// Gets an optional initial drawing color that is independent of the
    /// register file until the first selecting DECGCI command.
    /// </summary>
    public Rgba32? InitialDrawingColor { get; init; }

    /// <summary>
    /// Gets whether a complete sequence with no rasterable extent is discarded
    /// instead of retained as a geometry-only placement.
    /// </summary>
    public bool RejectZeroExtentGraphics { get; init; }

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
    /// Resolves a parsed pixel aspect ratio for this compatibility profile.
    /// </summary>
    public SixelAspectRatio ResolveAspectRatio(SixelAspectRatio aspect) =>
        AspectBehavior == SixelAspectBehavior.SquarePixels
            ? new SixelAspectRatio(1, 1)
            : aspect;

    /// <summary>
    /// Resolves a zero DECGRI count for this compatibility profile.
    /// </summary>
    public int ResolveRepeatCount(int repeatCount) =>
        repeatCount == 0 && ZeroRepeatBehavior == SixelZeroRepeatBehavior.RepeatOnce
            ? 1
            : repeatCount;

    /// <summary>
    /// Gets whether the supplied palette command changes the selected register.
    /// </summary>
    public bool SelectsColorRegister(SixelPaletteCommand command) =>
        !command.IsDefinition ||
        ColorDefinitionBehavior == SixelColorDefinitionBehavior.DefineAndSelect;

    /// <summary>
    /// Gets the maximum number of logical pixels a single graphic may materialize.
    /// </summary>
    public long MaximumRasterPixels { get; init; } = 16L * 1024 * 1024;

    /// <summary>
    /// Gets the maximum number of pixel writes performed while rasterizing.
    /// </summary>
    public long MaximumRasterOperations { get; init; } = 64L * 1024 * 1024;

    /// <summary>
    /// Gets the edge length of a sparse raster tile.
    /// </summary>
    public int RasterTileSize { get; init; } = 64;

    /// <summary>
    /// Gets the maximum number of sparse tiles retained for a single graphic.
    /// </summary>
    /// <remarks>
    /// Tiles contain up to <see cref="RasterTileSize"/> squared pixels in
    /// row-major order. Deriving this value from the raster pixel limit keeps
    /// the storage bound consistent for every permitted aspect ratio.
    /// </remarks>
    public int MaximumRasterTiles
    {
        get
        {
            var tilePixels = checked((long)RasterTileSize * RasterTileSize);
            var required = ((MaximumRasterPixels - 1) / tilePixels) + 1;
            return (int)Math.Min(required, int.MaxValue);
        }
    }

    /// <summary>Gets the maximum number of live placements retained per screen.</summary>
    public int MaximumPlacementsPerScreen { get; init; } = 4_096;

    /// <summary>Gets the maximum number of placement fragments retained in scrollback.</summary>
    public int MaximumHistoryPlacements { get; init; } = 4_096;

    /// <summary>Gets the maximum number of distinct images retained per screen.</summary>
    public int MaximumImagesPerScreen { get; init; } = 1_024;

    /// <summary>
    /// Gets the maximum aggregate logical pixel area retained by distinct images
    /// on one screen. Sparse storage remains allocation-proportional to painted
    /// tiles, while this bound also limits worst-case later dense materialization.
    /// </summary>
    public long MaximumRetainedLogicalPixelsPerScreen { get; init; } = 64L * 1024 * 1024;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumRetainedDcsBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumDcsHeaderParameters, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumNumericValue, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumRetainedCommands, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumPaletteMutations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumDiagnostics, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(ColorRegisterCount, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(InitialColorRegister);
        if (InitialColorRegister >= ColorRegisterCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(InitialColorRegister),
                InitialColorRegister,
                "The initial color register must be inside the configured register file.");
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumRasterPixels, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumRasterOperations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(RasterTileSize, 1);
        if ((long)RasterTileSize * RasterTileSize > Array.MaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RasterTileSize),
                RasterTileSize,
                "The raster tile must not exceed the maximum supported array length.");
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumPlacementsPerScreen, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumHistoryPlacements, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumImagesPerScreen, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumRetainedLogicalPixelsPerScreen, 1);
    }
}
