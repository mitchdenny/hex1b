using Hex1b.Input;
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
    /// <summary>Rebindable action: Scroll up one line in the scrollback buffer.</summary>
    public static readonly ActionId ScrollUpLine = new($"{nameof(TerminalWidget)}.{nameof(ScrollUpLine)}");
    /// <summary>Rebindable action: Scroll down one line in the scrollback buffer.</summary>
    public static readonly ActionId ScrollDownLine = new($"{nameof(TerminalWidget)}.{nameof(ScrollDownLine)}");
    /// <summary>Rebindable action: Scroll up one page in the scrollback buffer.</summary>
    public static readonly ActionId ScrollUpPage = new($"{nameof(TerminalWidget)}.{nameof(ScrollUpPage)}");
    /// <summary>Rebindable action: Scroll down one page in the scrollback buffer.</summary>
    public static readonly ActionId ScrollDownPage = new($"{nameof(TerminalWidget)}.{nameof(ScrollDownPage)}");
    /// <summary>Rebindable action: Scroll to the top of the scrollback buffer.</summary>
    public static readonly ActionId ScrollToTop = new($"{nameof(TerminalWidget)}.{nameof(ScrollToTop)}");
    /// <summary>Rebindable action: Scroll to the bottom, returning to the live terminal view.</summary>
    public static readonly ActionId ScrollToBottom = new($"{nameof(TerminalWidget)}.{nameof(ScrollToBottom)}");

    // Copy mode actions
    /// <summary>Rebindable action: Enter copy mode for text selection.</summary>
    public static readonly ActionId EnterCopyMode = new($"{nameof(TerminalWidget)}.{nameof(EnterCopyMode)}");
    /// <summary>Rebindable action: Move the copy mode cursor up one row.</summary>
    public static readonly ActionId CopyModeUp = new($"{nameof(TerminalWidget)}.{nameof(CopyModeUp)}");
    /// <summary>Rebindable action: Move the copy mode cursor down one row.</summary>
    public static readonly ActionId CopyModeDown = new($"{nameof(TerminalWidget)}.{nameof(CopyModeDown)}");
    /// <summary>Rebindable action: Move the copy mode cursor left one column.</summary>
    public static readonly ActionId CopyModeLeft = new($"{nameof(TerminalWidget)}.{nameof(CopyModeLeft)}");
    /// <summary>Rebindable action: Move the copy mode cursor right one column.</summary>
    public static readonly ActionId CopyModeRight = new($"{nameof(TerminalWidget)}.{nameof(CopyModeRight)}");
    /// <summary>Rebindable action: Move the copy mode cursor forward one word.</summary>
    public static readonly ActionId CopyModeWordForward = new($"{nameof(TerminalWidget)}.{nameof(CopyModeWordForward)}");
    /// <summary>Rebindable action: Move the copy mode cursor backward one word.</summary>
    public static readonly ActionId CopyModeWordBackward = new($"{nameof(TerminalWidget)}.{nameof(CopyModeWordBackward)}");
    /// <summary>Rebindable action: Move the copy mode cursor up one page.</summary>
    public static readonly ActionId CopyModePageUp = new($"{nameof(TerminalWidget)}.{nameof(CopyModePageUp)}");
    /// <summary>Rebindable action: Move the copy mode cursor down one page.</summary>
    public static readonly ActionId CopyModePageDown = new($"{nameof(TerminalWidget)}.{nameof(CopyModePageDown)}");
    /// <summary>Rebindable action: Move the copy mode cursor to the start of the current line.</summary>
    public static readonly ActionId CopyModeLineStart = new($"{nameof(TerminalWidget)}.{nameof(CopyModeLineStart)}");
    /// <summary>Rebindable action: Move the copy mode cursor to the end of the current line.</summary>
    public static readonly ActionId CopyModeLineEnd = new($"{nameof(TerminalWidget)}.{nameof(CopyModeLineEnd)}");
    /// <summary>Rebindable action: Move the copy mode cursor to the top of the buffer.</summary>
    public static readonly ActionId CopyModeBufferTop = new($"{nameof(TerminalWidget)}.{nameof(CopyModeBufferTop)}");
    /// <summary>Rebindable action: Move the copy mode cursor to the bottom of the buffer.</summary>
    public static readonly ActionId CopyModeBufferBottom = new($"{nameof(TerminalWidget)}.{nameof(CopyModeBufferBottom)}");
    /// <summary>Rebindable action: Start or toggle character selection in copy mode.</summary>
    public static readonly ActionId CopyModeStartSelection = new($"{nameof(TerminalWidget)}.{nameof(CopyModeStartSelection)}");
    /// <summary>Rebindable action: Toggle line selection mode in copy mode.</summary>
    public static readonly ActionId CopyModeToggleLineMode = new($"{nameof(TerminalWidget)}.{nameof(CopyModeToggleLineMode)}");
    /// <summary>Rebindable action: Toggle block/rectangular selection mode in copy mode.</summary>
    public static readonly ActionId CopyModeToggleBlockMode = new($"{nameof(TerminalWidget)}.{nameof(CopyModeToggleBlockMode)}");
    /// <summary>Rebindable action: Copy the selected text and exit copy mode.</summary>
    public static readonly ActionId CopyModeCopy = new($"{nameof(TerminalWidget)}.{nameof(CopyModeCopy)}");
    /// <summary>Rebindable action: Cancel copy mode without copying.</summary>
    public static readonly ActionId CopyModeCancel = new($"{nameof(TerminalWidget)}.{nameof(CopyModeCancel)}");

    /// <summary>
    /// Gets the callback that builds a fallback widget when the terminal is not running.
    /// </summary>
    internal Func<TerminalNotRunningArgs, Hex1bWidget>? NotRunningBuilder { get; init; }
    
    /// <summary>
    /// Gets the number of rows to scroll per mouse wheel tick. Defaults to 3.
    /// </summary>
    public int MouseWheelScrollAmount { get; init; } = 3;
    
    /// <summary>
    /// Gets the copy mode bindings options, or null if copy mode bindings are not configured.
    /// Set via <see cref="TerminalExtensions.CopyModeBindings"/>.
    /// </summary>
    internal CopyModeBindingsOptions? CopyModeOptions { get; init; }
    
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
        node.MouseWheelScrollAmount = MouseWheelScrollAmount;
        
        // Set the invalidate callback so the node can trigger re-renders when output arrives
        if (context.InvalidateCallback != null)
        {
            node.SetInvalidateCallback(context.InvalidateCallback);
        }
        
        // Set the capture callbacks so the node can manage input capture
        node.SetCaptureCallbacks(context.CaptureInputCallback, context.ReleaseCaptureCallback);
        
        // Bind to the new handle
        node.Bind();
        
        // Attach copy mode helper if configured
        node.AttachCopyModeHelper(CopyModeOptions);
        
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
