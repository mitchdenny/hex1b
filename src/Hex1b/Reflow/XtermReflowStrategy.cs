namespace Hex1b.Reflow;

/// <summary>
/// Reflow strategy matching xterm behavior. xterm does NOT support text reflow —
/// content is cropped or extended on resize without re-wrapping soft-wrapped lines.
/// </summary>
/// <remarks>
/// <para>
/// xterm is a traditional terminal emulator that does not reflow text on resize.
/// It sends SIGWINCH to the application, which is responsible for redrawing.
/// This strategy delegates to <see cref="NoReflowStrategy"/> for the actual resize behavior.
/// </para>
/// <para>
/// Historically, <c>XtermReflowStrategy</c> implemented bottom-fill reflow. That behavior
/// is now provided by <see cref="AlacrittyReflowStrategy"/> and <see cref="WindowsTerminalReflowStrategy"/>.
/// </para>
/// </remarks>
public sealed class XtermReflowStrategy : ITerminalReflowProvider
{
    /// <summary>
    /// Shared singleton instance.
    /// </summary>
    public static readonly XtermReflowStrategy Instance = new();

    /// <inheritdoc/>
    public bool ShouldClearSoftWrapOnAbsolutePosition => false;

    /// <inheritdoc/>
    public ReflowResult Reflow(ReflowContext context)
    {
        return NoReflowStrategy.Instance.Reflow(context);
    }
}
