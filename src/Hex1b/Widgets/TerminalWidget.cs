using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Arguments provided to the WhenNotRunning callback.
/// </summary>
/// <param name="Handle">The terminal handle.</param>
/// <param name="State">The current terminal state.</param>
/// <param name="ExitCode">The exit code if the terminal completed, null otherwise.</param>
public sealed record TerminalNotRunningArgs(
    TerminalWidgetHandle Handle,
    TerminalState State,
    int? ExitCode);

/// <summary>
/// A widget that displays an embedded terminal session.
/// </summary>
/// <remarks>
/// <para>
/// The TerminalWidget binds to a <see cref="TerminalWidgetHandle"/> which provides
/// the screen buffer from a running terminal session. This allows embedding
/// child terminals within a TUI application.
/// </para>
/// <para>
/// When the terminal is not running (not started or completed), you can optionally
/// provide a fallback widget using <see cref="TerminalExtensions.WhenNotRunning"/>. This enables
/// post-exit interactivity such as showing the exit code or a "restart" button.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// var terminal = Hex1bTerminal.CreateBuilder()
///     .WithPtyProcess("bash")
///     .WithTerminalWidget(out var bashHandle)
///     .Build();
/// 
/// _ = terminal.RunAsync(appCt);
/// 
/// ctx.Terminal(bashHandle)
///    .WhenNotRunning(args => ctx.Text($"Exited with code {args.ExitCode}"));
/// </code>
/// </para>
/// </remarks>
public sealed record TerminalWidget(TerminalWidgetHandle Handle) : Hex1bWidget
{
    /// <summary>
    /// Gets the callback that builds a fallback widget when the terminal is not running.
    /// </summary>
    internal Func<TerminalNotRunningArgs, Hex1bWidget>? NotRunningBuilder { get; init; }
    
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TerminalNode ?? new TerminalNode();
        
        // Unbind from previous handle if different
        if (node.Handle != null && node.Handle != Handle)
        {
            node.Unbind();
        }
        
        node.Handle = Handle;
        node.SourceWidget = this;
        node.NotRunningBuilder = NotRunningBuilder;
        
        // Set the invalidate callback so the node can trigger re-renders when output arrives
        if (context.InvalidateCallback != null)
        {
            node.SetInvalidateCallback(context.InvalidateCallback);
        }
        
        // Bind to the new handle
        node.Bind();
        
        // If terminal is not running and we have a fallback builder, reconcile the fallback widget
        if (Handle.State != TerminalState.Running && NotRunningBuilder != null)
        {
            var args = new TerminalNotRunningArgs(Handle, Handle.State, Handle.ExitCode);
            var fallbackWidget = NotRunningBuilder(args);
            node.FallbackChild = await fallbackWidget.ReconcileAsync(node.FallbackChild, context);
        }
        else
        {
            node.FallbackChild = null;
        }
        
        return node;
    }
    
    internal override Type GetExpectedNodeType() => typeof(TerminalNode);
}
