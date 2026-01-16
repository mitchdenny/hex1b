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
    
    /// <summary>
    /// Specifies a fallback widget to display when the terminal is not running.
    /// </summary>
    /// <param name="widget">The TerminalWidget to configure.</param>
    /// <param name="builder">A callback that builds the fallback widget. 
    /// Receives the terminal state and exit code (if completed).</param>
    /// <returns>The configured TerminalWidget.</returns>
    /// <remarks>
    /// <para>
    /// This enables post-exit interactivity. When the terminal process exits,
    /// the fallback widget is displayed instead of the terminal buffer.
    /// Common use cases include:
    /// <list type="bullet">
    ///   <item>Showing the exit code</item>
    ///   <item>Providing a "restart" button</item>
    ///   <item>Displaying a message while waiting for the terminal to start</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// ctx.Terminal(bashHandle)
    ///    .WhenNotRunning(args => ctx.VStack(v => [
    ///        v.Text($"Terminal exited with code {args.ExitCode}"),
    ///        v.Button("Restart").OnClick(_ => RestartTerminal())
    ///    ]));
    /// </code>
    /// </example>
    public static TerminalWidget WhenNotRunning(
        this TerminalWidget widget,
        Func<TerminalNotRunningArgs, Hex1bWidget> builder)
        => widget with { NotRunningBuilder = builder };
}
