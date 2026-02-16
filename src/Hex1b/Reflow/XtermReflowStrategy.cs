namespace Hex1b.Reflow;

/// <summary>
/// Reflow strategy using bottom-fill cursor behavior. Soft-wrapped lines are re-wrapped
/// to the new width on resize. Content is filled from the bottom of the screen, pushing
/// content upward. Absolute cursor positioning breaks the reflow chain.
/// </summary>
/// <remarks>
/// <para>
/// Despite the name, xterm itself does not support text reflow. This strategy is named
/// for its bottom-fill behavior which is used by terminals like Alacritty and Windows Terminal.
/// The saved cursor (DECSC) is NOT reflowed by this strategy.
/// </para>
/// </remarks>
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
