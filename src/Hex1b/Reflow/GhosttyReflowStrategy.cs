namespace Hex1b.Reflow;

/// <summary>
/// Reflow strategy matching Ghostty terminal behavior. Post-1.1.1, Ghostty's reflow
/// is functionally identical to VTE: cursor-anchored with saved cursor reflow.
/// Kept as a separate class for auto-detection mapping purposes.
/// </summary>
/// <remarks>
/// <para>
/// Ghostty reflows both the primary cursor and the saved cursor during resize,
/// matching VTE behavior. The cursor row is anchored to its current visual position.
/// </para>
/// <para>
/// Reference: <c>ghostty-org/ghostty</c> — <c>src/terminal/Screen.zig</c> resize logic.
/// </para>
/// </remarks>
public sealed class GhosttyReflowStrategy : ITerminalReflowProvider
{
    /// <summary>
    /// Shared singleton instance.
    /// </summary>
    public static readonly GhosttyReflowStrategy Instance = new();

    /// <inheritdoc/>
    public bool ShouldClearSoftWrapOnAbsolutePosition => true;

    /// <inheritdoc/>
    public ReflowResult Reflow(ReflowContext context)
    {
        if (context.InAlternateScreen)
        {
            // Ghostty does not reflow the alternate screen buffer
            return NoReflowStrategy.Instance.Reflow(context);
        }

        return ReflowHelper.PerformReflow(context, preserveCursorRow: true, reflowSavedCursor: true);
    }
}
