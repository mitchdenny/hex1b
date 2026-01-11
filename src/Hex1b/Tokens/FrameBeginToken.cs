namespace Hex1b.Tokens;

/// <summary>
/// Signals the beginning of a render frame for delta encoding optimization.
/// </summary>
/// <remarks>
/// <para>
/// This token is used internally by Hex1bApp to bracket render operations.
/// When a <see cref="Hex1bAppRenderOptimizationFilter"/> (or similar filter) receives this token,
/// it enters "buffering mode" where updates are accumulated but not emitted until
/// the corresponding <see cref="FrameEndToken"/> is received.
/// </para>
/// <para>
/// This prevents intermediate states (like clearing a region before re-rendering)
/// from causing visible flicker - only the final frame state is transmitted.
/// </para>
/// <para>
/// Uses APC (Application Program Command) format: ESC _ HEX1BAPP:FRAME:BEGIN ESC \
/// </para>
/// </remarks>
public sealed record FrameBeginToken : AnsiToken
{
    /// <summary>
    /// Singleton instance of the frame begin token.
    /// </summary>
    public static readonly FrameBeginToken Instance = new();
    
    private FrameBeginToken() { }
}
