namespace Hex1b.Reflow;

/// <summary>
/// Reflow strategy matching xterm behavior. Soft-wrapped lines are re-wrapped
/// to the new width on resize. Content is filled from the bottom of the screen.
/// Absolute cursor positioning breaks the reflow chain.
/// </summary>
public sealed class XtermReflowStrategy : ITerminalReflowProvider
{
    /// <summary>
    /// Shared singleton instance.
    /// </summary>
    public static readonly XtermReflowStrategy Instance = new();

    /// <inheritdoc/>
    public bool ShouldClearSoftWrapOnAbsolutePosition => true;

    /// <inheritdoc/>
    public ReflowResult Reflow(ReflowContext context)
    {
        if (context.InAlternateScreen)
        {
            // xterm does not reflow the alternate screen buffer
            return NoReflowStrategy.Instance.Reflow(context);
        }

        return ReflowHelper.PerformReflow(context, preserveCursorRow: false);
    }
}
