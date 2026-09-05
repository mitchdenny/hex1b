/// <summary>
/// The cold-to-hot ramp the dust cloud paints with, as 8-bit RGB triples.
/// </summary>
/// <remarks>
/// The ramp runs cold to hot: slow motes drifting in the open field are dark blue,
/// and motes freshly thrown by a collapse glow red-white. Values are kept at full
/// 8-bit depth here because that is what KGP transmits; the Sixel renderer narrows
/// them to the 0-100 range its colour registers use, which round-trips exactly for
/// every entry below.
/// </remarks>
internal static class CloudPalette
{
    /// <summary>Cold-to-hot ramp, ordered from slowest to fastest.</summary>
    public static IReadOnlyList<(byte Red, byte Green, byte Blue)> Colors { get; } =
    [
        (20, 26, 87),
        (31, 51, 153),
        (41, 87, 219),
        (61, 140, 255),
        (115, 204, 255),
        (199, 245, 255),
        (255, 235, 184),
        (255, 189, 92),
        (255, 128, 41),
        (255, 66, 31),
        (255, 31, 51),
        (255, 158, 158),
    ];

    /// <summary>
    /// Maps a normalised heat value in [0,1] onto a palette index.
    /// </summary>
    public static int IndexForHeat(double heat)
    {
        var scaled = (int)(heat * Colors.Count);
        return Math.Clamp(scaled, 0, Colors.Count - 1);
    }
}
