namespace Hex1b.Reflow;

/// <summary>
/// Reflow strategy matching Foot terminal emulator behavior. Soft-wrapped lines
/// are re-wrapped to the new width on resize. The cursor row is anchored to its
/// current visual position. The DECSC saved cursor is also reflowed.
/// </summary>
/// <remarks>
/// <para>
/// Foot reflows both the primary cursor and the saved cursor (DECSC/ESC 7) during resize,
/// matching VTE behavior. Content in the alternate screen buffer is NOT reflowed.
/// </para>
/// <para>
/// Reference: <c>dnkl/foot</c> — Wayland-native terminal emulator.
/// </para>
/// </remarks>
public sealed class FootReflowStrategy : ITerminalReflowProvider
{
    /// <summary>
    /// Shared singleton instance.
    /// </summary>
    public static readonly FootReflowStrategy Instance = new();

    /// <inheritdoc/>
    public bool ShouldClearSoftWrapOnAbsolutePosition => true;

    /// <inheritdoc/>
    public ReflowResult Reflow(ReflowContext context)
    {
        if (context.InAlternateScreen)
        {
            return NoReflowStrategy.Instance.Reflow(context);
        }

        return ReflowHelper.PerformReflow(context, preserveCursorRow: true, reflowSavedCursor: true);
    }
}
