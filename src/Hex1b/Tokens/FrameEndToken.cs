namespace Hex1b.Tokens;

/// <summary>
/// Signals the end of a render frame for delta encoding optimization.
/// </summary>
/// <remarks>
/// <para>
/// This token is used internally by Hex1bApp to bracket render operations.
/// When a <see cref="Hex1bAppRenderOptimizationFilter"/> (or similar filter) receives this token,
/// it exits "buffering mode", compares the accumulated buffer against the committed
/// state, and emits only the cells that actually changed.
/// </para>
/// <para>
/// This ensures that intermediate states (like clearing followed by rendering)
/// are collapsed into a single update, eliminating flicker.
/// </para>
/// <para>
/// Uses APC (Application Program Command) format: ESC _ HEX1BAPP:FRAME:END ESC \
/// </para>
/// </remarks>
public sealed record FrameEndToken : AnsiToken
{
    /// <summary>
    /// Singleton instance of the frame end token.
    /// </summary>
    public static readonly FrameEndToken Instance = new();
    
    private FrameEndToken() { }
}
