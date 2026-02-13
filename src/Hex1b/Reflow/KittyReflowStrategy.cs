namespace Hex1b.Reflow;

/// <summary>
/// Reflow strategy matching kitty terminal behavior. Soft-wrapped lines are
/// re-wrapped to the new width on resize. The cursor row is anchored to its
/// current visual position. Absolute cursor positioning breaks the reflow chain.
/// </summary>
public sealed class KittyReflowStrategy : ITerminalReflowProvider
{
    /// <summary>
    /// Shared singleton instance.
    /// </summary>
    public static readonly KittyReflowStrategy Instance = new();

    /// <inheritdoc/>
    public bool ShouldClearSoftWrapOnAbsolutePosition => true;

    /// <inheritdoc/>
    public ReflowResult Reflow(ReflowContext context)
    {
        if (context.InAlternateScreen)
        {
            // kitty does not reflow the alternate screen buffer
            return NoReflowStrategy.Instance.Reflow(context);
        }

        return ReflowHelper.PerformReflow(context, preserveCursorRow: true);
    }
}
