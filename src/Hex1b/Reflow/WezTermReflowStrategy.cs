namespace Hex1b.Reflow;

/// <summary>
/// Reflow strategy matching WezTerm terminal behavior. Soft-wrapped lines are
/// re-wrapped to the new width on resize. The cursor row is anchored to its
/// current visual position.
/// </summary>
/// <remarks>
/// <para>
/// WezTerm reflows text and the primary cursor during resize, but the saved cursor
/// (DECSC/ESC 7) reflow has known bugs — the restored position can be incorrect after
/// resize. For now, saved cursor reflow is disabled to match observed behavior.
/// </para>
/// <para>
/// Reference: <c>wezterm/wezterm</c> — GitHub issues #1821, #6669.
/// </para>
/// </remarks>
public sealed class WezTermReflowStrategy : ITerminalReflowProvider
{
    /// <summary>
    /// Shared singleton instance.
    /// </summary>
    public static readonly WezTermReflowStrategy Instance = new();

    /// <inheritdoc/>
    public bool ShouldClearSoftWrapOnAbsolutePosition => true;

    /// <inheritdoc/>
    public ReflowResult Reflow(ReflowContext context)
    {
        if (context.InAlternateScreen)
        {
            return NoReflowStrategy.Instance.Reflow(context);
        }

        return ReflowHelper.PerformReflow(context, preserveCursorRow: true);
    }
}
