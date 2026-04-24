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
    
    /// <summary>
    /// Sets the number of rows to scroll per mouse wheel tick.
    /// </summary>
    /// <param name="widget">The TerminalWidget to configure.</param>
    /// <param name="rows">Number of rows per scroll tick. Defaults to 3.</param>
    /// <returns>The configured TerminalWidget.</returns>
    public static TerminalWidget WithMouseWheelScrollAmount(
        this TerminalWidget widget,
        int rows)
        => widget with { MouseWheelScrollAmount = rows };
    
    /// <summary>
    /// Enables standard copy mode bindings with configurable key and mouse mappings.
    /// Provides vi-style keyboard navigation, character/line/block selection,
    /// and mouse drag-to-select with modifier keys.
    /// </summary>
    /// <param name="widget">The TerminalWidget to configure.</param>
    /// <param name="configure">Optional callback to customize the default bindings.</param>
    /// <returns>The configured TerminalWidget.</returns>
    /// <example>
    /// <description>Enable copy mode with default vi-style bindings:</description>
    /// <code>
    /// ctx.Terminal(handle).CopyModeBindings().Fill()
    /// </code>
    /// </example>
    /// <example>
    /// <description>Customize the entry key:</description>
    /// <code>
    /// ctx.Terminal(handle).CopyModeBindings(options =>
    /// {
    ///     options.EnterKeys = [Hex1bKey.F5];
    /// }).Fill()
    /// </code>
    /// </example>
    public static TerminalWidget CopyModeBindings(
        this TerminalWidget widget,
        Action<CopyModeBindingsOptions>? configure = null)
    {
        var options = new CopyModeBindingsOptions();
        configure?.Invoke(options);
        return widget with { CopyModeOptions = options };
    }
}
