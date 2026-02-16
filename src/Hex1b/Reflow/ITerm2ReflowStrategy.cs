namespace Hex1b.Reflow;

/// <summary>
/// Reflow strategy matching iTerm2 terminal behavior. iTerm2 does NOT support true
/// text reflow — existing wrapped lines do not merge when the terminal is widened.
/// </summary>
/// <remarks>
/// <para>
/// When narrowing, iTerm2 re-wraps visible text to fit. However, when widening,
/// previously wrapped lines remain fixed in the scrollback. This asymmetric behavior
/// means iTerm2 does not provide reliable full reflow, so we treat it as no-reflow.
/// </para>
/// <para>
/// Reference: iTerm2 FAQ and documentation — <c>iterm2.com</c>.
/// </para>
/// </remarks>
public sealed class ITerm2ReflowStrategy : ITerminalReflowProvider
{
    /// <summary>
    /// Shared singleton instance.
    /// </summary>
    public static readonly ITerm2ReflowStrategy Instance = new();

    /// <inheritdoc/>
    public bool ShouldClearSoftWrapOnAbsolutePosition => false;

    /// <inheritdoc/>
    public ReflowResult Reflow(ReflowContext context)
    {
        return NoReflowStrategy.Instance.Reflow(context);
    }
}
