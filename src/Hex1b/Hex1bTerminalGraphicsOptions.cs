namespace Hex1b;

/// <summary>
/// Configures resource limits for graphics retained and processed by a
/// <see cref="Hex1bTerminal"/>.
/// </summary>
/// <remarks>
/// <para>
/// These limits are protocol-neutral. A graphics protocol uses the limits that
/// apply to its representation; unsupported concepts are ignored rather than
/// translated between protocols.
/// </para>
/// <para>
/// Per-image limits bound the work accepted from one graphic. Per-screen limits
/// bound the live terminal state independently for the main and alternate
/// screens. Snapshot owners may extend the lifetime of resources after the live
/// terminal has evicted them.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await using var terminal = Hex1bTerminal.CreateBuilder()
///     .WithPtyProcess("bash")
///     .WithGraphics(options =>
///     {
///         options.MaximumRetainedBytesPerScreen = 64L * 1024 * 1024;
///         options.MaximumImagesPerScreen = 256;
///     })
///     .Build();
///
/// await terminal.RunAsync();
/// </code>
/// </example>
public sealed class Hex1bTerminalGraphicsOptions
{
    /// <summary>
    /// Gets or sets the maximum number of source input bytes retained for one
    /// graphic. The default is 1 MiB.
    /// </summary>
    /// <remarks>
    /// Framing may continue after this limit is reached so the terminal can
    /// recover at the protocol terminator. This limit is separate from decoded
    /// raster work and aggregate retained-memory budgets.
    /// For zlib-compressed KGP uploads this bounds Base64-decoded compressed
    /// input, including uninterpreted bytes after the first complete zlib member.
    /// Legacy uncompressed KGP admission is unchanged.
    /// </remarks>
    public int MaximumRetainedInputBytesPerImage { get; set; } = 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum logical pixel area rasterized for one graphic.
    /// The default is 16,777,216 pixels.
    /// </summary>
    /// <remarks>Also bounds the dimensions of zlib-compressed KGP images.</remarks>
    public long MaximumRasterPixelsPerImage { get; set; } = 16L * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum number of pixel-write operations performed while
    /// rasterizing one graphic. The default is 67,108,864 operations.
    /// </summary>
    public long MaximumRasterOperationsPerImage { get; set; } = 64L * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum number of distinct images retained by one screen.
    /// The default is 1,024.
    /// </summary>
    public int MaximumImagesPerScreen { get; set; } = 1_024;

    /// <summary>
    /// Gets or sets the maximum number of live image placements retained by one
    /// screen. The default is 4,096.
    /// </summary>
    public int MaximumPlacementsPerScreen { get; set; } = 4_096;

    /// <summary>
    /// Gets or sets the maximum number of image placement fragments retained in
    /// scrollback history. The default is 4,096.
    /// </summary>
    public int MaximumHistoryPlacements { get; set; } = 4_096;

    /// <summary>
    /// Gets or sets the maximum aggregate logical pixel area retained by distinct
    /// images on one screen. The default is 67,108,864 pixels.
    /// </summary>
    /// <remarks>
    /// This bounds potential dense raster materialization independently from the
    /// actual retained-byte budget.
    /// </remarks>
    public long MaximumRetainedLogicalPixelsPerScreen { get; set; } =
        64L * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum aggregate encoded and decoded image data retained
    /// by one screen. The default is 335,544,320 bytes.
    /// </summary>
    /// <remarks>
    /// The main and alternate screens each receive one shared budget across all
    /// graphics protocols. KGP counts encoded image and animation-frame bytes.
    /// A zlib-backed static KGP image also reserves its validated decoded-format
    /// length, without retaining a decoded array. Materialized animation frames
    /// count actual bytes only. This reservation bounds live admission, not
    /// caller-owned arrays returned by <see cref="KgpImageData.Data"/> or old snapshots.
    /// Sixel counts each distinct image once, including retained payload, parsed
    /// metadata, sparse raster tiles, and a cached dense pixel buffer. Each
    /// protocol applies its established oldest-first eviction order to its own
    /// resources; a new resource is rejected when it cannot fit after those
    /// evictions, without destroying another protocol's placements. Lazy Sixel
    /// cache growth is non-destructive and remains uncached when it cannot fit.
    /// A value of zero disables retained byte-backed image data. Per-image input
    /// and raster limits remain independent safety bounds.
    /// </remarks>
    public long MaximumRetainedBytesPerScreen { get; set; } = 320L * 1024 * 1024;

    internal Hex1bTerminalGraphicsOptions Clone() => new()
    {
        MaximumRetainedInputBytesPerImage = MaximumRetainedInputBytesPerImage,
        MaximumRasterPixelsPerImage = MaximumRasterPixelsPerImage,
        MaximumRasterOperationsPerImage = MaximumRasterOperationsPerImage,
        MaximumImagesPerScreen = MaximumImagesPerScreen,
        MaximumPlacementsPerScreen = MaximumPlacementsPerScreen,
        MaximumHistoryPlacements = MaximumHistoryPlacements,
        MaximumRetainedLogicalPixelsPerScreen = MaximumRetainedLogicalPixelsPerScreen,
        MaximumRetainedBytesPerScreen = MaximumRetainedBytesPerScreen,
    };

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumRetainedInputBytesPerImage);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumRasterPixelsPerImage, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            MaximumRasterPixelsPerImage,
            int.MaxValue);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumRasterOperationsPerImage, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumImagesPerScreen, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumPlacementsPerScreen, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumHistoryPlacements, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumRetainedLogicalPixelsPerScreen, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumRetainedBytesPerScreen);
    }
}
