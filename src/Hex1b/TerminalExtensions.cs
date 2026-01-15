using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating TerminalWidget.
/// </summary>
public static class TerminalExtensions
{
    /// <summary>
    /// Creates a TerminalWidget that displays the content of the specified terminal handle.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="handle">The terminal handle to display.</param>
    /// <returns>A new TerminalWidget bound to the handle.</returns>
    /// <example>
    /// <code>
    /// var terminal = Hex1bTerminal.CreateBuilder()
    ///     .WithPtyProcess("bash")
    ///     .WithTerminalWidget(out var bashHandle)
    ///     .Build();
    /// 
    /// _ = terminal.RunAsync(appCt);
    /// 
    /// ctx.Terminal(bashHandle);
    /// </code>
    /// </example>
    public static TerminalWidget Terminal<TParent>(
        this WidgetContext<TParent> ctx,
        TerminalWidgetHandle handle)
        where TParent : Hex1bWidget
        => new(handle);
}
