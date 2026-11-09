namespace Hex1b.Surfaces;

/// <summary>
/// Hands out shared string instances for grapheme clusters written to surface cells, so that
/// writing the same character again does not allocate a new string for every cell.
/// </summary>
/// <remarks>
/// <see cref="SurfaceCell.Character"/> is a string, so a cell cannot hold a span. Printable ASCII
/// comes from a table that is built once and never changes. Other clusters (box drawing, CJK,
/// emoji) come from a small direct-mapped cache that is private to each thread, so no locking
/// is needed and the instances are never mutated.
/// </remarks>
internal static class GraphemeStringCache
{
    private const int SlotCount = 512; // Power of two, used as a mask.
    private const int MaxCachedLength = 16;

    private static readonly string[] s_ascii = CreateAsciiTable();

    [ThreadStatic]
    private static string?[]? t_slots;

    /// <summary>
    /// Gets the string for a printable ASCII character (0x20-0x7E).
    /// </summary>
    public static string GetAscii(char ch) => s_ascii[ch];

    /// <summary>
    /// Gets a string with the contents of <paramref name="grapheme"/>, reusing an earlier instance
    /// of the same contents when the thread still has it cached.
    /// </summary>
    public static string Get(ReadOnlySpan<char> grapheme)
    {
        if (grapheme.Length == 1 && grapheme[0] < s_ascii.Length)
            return s_ascii[grapheme[0]];

        if (grapheme.Length == 0 || grapheme.Length > MaxCachedLength)
            return new string(grapheme);

        var hash = 17;
        foreach (var c in grapheme)
            hash = unchecked(hash * 31 + c);

        var slots = t_slots ??= new string?[SlotCount];
        var index = hash & (SlotCount - 1);
        var cached = slots[index];
        if (cached is not null && grapheme.SequenceEqual(cached))
            return cached;

        var created = new string(grapheme);
        slots[index] = created;
        return created;
    }

    private static string[] CreateAsciiTable()
    {
        var table = new string[128];
        for (var i = 0; i < table.Length; i++)
            table[i] = ((char)i).ToString();
        return table;
    }
}
