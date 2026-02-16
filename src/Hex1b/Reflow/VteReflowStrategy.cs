namespace Hex1b.Reflow;

/// <summary>
/// Reflow strategy matching VTE (GNOME Terminal, etc.) behavior. Soft-wrapped lines
/// are re-wrapped to the new width on resize. The cursor row is anchored to its
/// current visual position. Unlike kitty, the DECSC saved cursor is also reflowed.
/// Absolute cursor positioning breaks the reflow chain.
/// </summary>
/// <remarks>
/// <para>
/// VTE reflows both the primary cursor and the saved cursor (DECSC/ESC 7) during resize.
/// This ensures that applications which save/restore cursor position across redraws
/// (e.g., status bars, progress indicators) continue to work correctly after a terminal resize.
/// </para>
/// <para>
/// Reference: <c>GNOME/vte</c> — <c>doc/rewrap.txt</c> and <c>src/vte.cc</c> reflow logic.
/// </para>
/// </remarks>
public sealed class VteReflowStrategy : ITerminalReflowProvider
{
    /// <summary>
    /// Shared singleton instance.
    /// </summary>
    public static readonly VteReflowStrategy Instance = new();

    /// <inheritdoc/>
    public bool ShouldClearSoftWrapOnAbsolutePosition => true;

    /// <inheritdoc/>
    public ReflowResult Reflow(ReflowContext context)
    {
        if (context.InAlternateScreen)
        {
            // VTE does not reflow the alternate screen buffer
            return NoReflowStrategy.Instance.Reflow(context);
        }

        return ReflowHelper.PerformReflow(context, preserveCursorRow: true, reflowSavedCursor: true);
    }
}
