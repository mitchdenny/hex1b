namespace Hex1b.Reflow;

/// <summary>
/// Reflow strategy matching Alacritty terminal behavior. Soft-wrapped lines are
/// re-wrapped to the new width on resize. Content is bottom-filled: rows shift upward
/// to keep the bottom of the buffer visible.
/// </summary>
/// <remarks>
/// <para>
/// Alacritty has supported text reflow since v0.3.0. It uses bottom-fill cursor
/// positioning and does NOT reflow the saved cursor (DECSC).
/// </para>
/// <para>
/// Reference: <c>alacritty/alacritty</c> — <c>alacritty_terminal/src/grid/resize.rs</c>.
/// </para>
/// </remarks>
public sealed class AlacrittyReflowStrategy : ITerminalReflowProvider
{
    /// <summary>
    /// Shared singleton instance.
    /// </summary>
    public static readonly AlacrittyReflowStrategy Instance = new();

    /// <inheritdoc/>
    public bool ShouldClearSoftWrapOnAbsolutePosition => true;

    /// <inheritdoc/>
    public ReflowResult Reflow(ReflowContext context)
    {
        if (context.InAlternateScreen)
        {
            return NoReflowStrategy.Instance.Reflow(context);
        }

        return ReflowHelper.PerformReflow(context, preserveCursorRow: false);
    }
}
