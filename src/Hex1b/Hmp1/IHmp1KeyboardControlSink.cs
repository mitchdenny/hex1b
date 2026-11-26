namespace Hex1b;

/// <summary>
/// Receives the classified segments produced by <see cref="Hmp1KeyboardControlScanner"/>.
/// </summary>
internal interface IHmp1KeyboardControlSink
{
    /// <summary>Bytes that are not keyboard-relevant controls.</summary>
    void Text(ReadOnlySpan<byte> bytes);

    /// <summary>A complete Kitty keyboard control (<c>CSI [?=&gt;&lt;] … u</c>).</summary>
    void Keyboard(ReadOnlySpan<byte> sequence);

    /// <summary>A complete DEC private mode set/reset naming 47, 1047, or 1049.</summary>
    void ScreenMode(ReadOnlySpan<byte> sequence);

    /// <summary>A full reset (<c>ESC c</c>).</summary>
    void FullReset(ReadOnlySpan<byte> sequence);
}
