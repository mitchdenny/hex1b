namespace Hex1b.Reflow;

/// <summary>
/// Reflow strategy matching Windows Terminal (ConPTY) behavior. Soft-wrapped lines
/// are re-wrapped to the new width on resize. Content is bottom-filled.
/// </summary>
/// <remarks>
/// <para>
/// Windows Terminal reflows text by default on resize. It uses bottom-fill cursor
/// positioning and does NOT reflow the saved cursor (DECSC). Reflow can be disabled
/// by the user via settings ("Wrap text output on resize").
/// </para>
/// <para>
/// Reference: <c>microsoft/terminal</c> — <c>src/buffer/out/textBuffer.cpp</c> Reflow method.
/// </para>
/// </remarks>
public sealed class WindowsTerminalReflowStrategy : ITerminalReflowProvider
{
    /// <summary>
    /// Shared singleton instance.
    /// </summary>
    public static readonly WindowsTerminalReflowStrategy Instance = new();

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
