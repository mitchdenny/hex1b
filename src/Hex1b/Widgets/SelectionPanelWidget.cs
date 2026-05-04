using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A container that wraps a child widget so that the user can later snapshot
/// or select over the rendered content. The eventual goal is a full copy/select
/// mode comparable to <see cref="TerminalWidget"/>'s, but today the widget only
/// supports proof-of-concept snapshot operations: each snapshot mode picks a
/// different fixed-geometry subset of the rendered cells and surfaces the
/// result via <see cref="OnSnapshot(System.Action{string})"/>.
/// </summary>
/// <remarks>
/// Layout, focus, and input handling are otherwise delegated to the child
/// unchanged. When a snapshot handler is registered, the panel installs
/// global bindings for each snapshot mode (defaults: <c>F7</c>
/// <see cref="SnapshotCells"/>, <c>F8</c> <see cref="SnapshotBlock"/>,
/// <c>F9</c> <see cref="SnapshotLines"/>, <c>F12</c> <see cref="Snapshot"/>).
/// Each binding renders the wrapped subtree to a private surface, reads back
/// the selected cells as plain text, and invokes the handler with the result.
/// Function keys are used by default because terminals reliably surface F-key
/// events (with optional modifiers) — letter keys with <c>Ctrl+Shift</c> are
/// folded to an ASCII control byte by Windows console and most xterm-style
/// terminals, so a chord like <c>Ctrl+Shift+S</c> is not distinguishable from
/// <c>Ctrl+S</c>. The defaults also dodge two well-known F-key conflicts:
/// <c>F6</c> is the <see cref="TerminalWidget"/> copy-mode default, and
/// <c>F11</c> toggles full screen in Windows Terminal (intercepted before
/// the app sees it).
/// </remarks>
/// <param name="Child">The child widget to wrap.</param>
public sealed record SelectionPanelWidget(Hex1bWidget Child) : Hex1bWidget
{
    /// <summary>Rebindable action: snapshot the entire wrapped content as plain text. Default: <c>F12</c>.</summary>
    public static readonly ActionId Snapshot = new($"{nameof(SelectionPanelWidget)}.{nameof(Snapshot)}");

    /// <summary>Rebindable action: snapshot a character-stream (terminal-style) slice. Default: <c>F7</c>.</summary>
    public static readonly ActionId SnapshotCells = new($"{nameof(SelectionPanelWidget)}.{nameof(SnapshotCells)}");

    /// <summary>Rebindable action: snapshot a rectangular block slice. Default: <c>F8</c>.</summary>
    public static readonly ActionId SnapshotBlock = new($"{nameof(SelectionPanelWidget)}.{nameof(SnapshotBlock)}");

    /// <summary>Rebindable action: snapshot a whole-line slice. Default: <c>F9</c>.</summary>
    public static readonly ActionId SnapshotLines = new($"{nameof(SelectionPanelWidget)}.{nameof(SnapshotLines)}");

    internal Func<string, Task>? SnapshotHandler { get; init; }

    /// <summary>
    /// Sets a synchronous snapshot handler. Invoked with the plain-text content
    /// of the snapshot whenever any of the snapshot actions fires
    /// (<see cref="Snapshot"/>, <see cref="SnapshotCells"/>,
    /// <see cref="SnapshotBlock"/>, <see cref="SnapshotLines"/>).
    /// </summary>
    public SelectionPanelWidget OnSnapshot(Action<string> handler)
        => this with { SnapshotHandler = text => { handler(text); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous snapshot handler. Invoked with the plain-text
    /// content of the snapshot whenever any of the snapshot actions fires
    /// (<see cref="Snapshot"/>, <see cref="SnapshotCells"/>,
    /// <see cref="SnapshotBlock"/>, <see cref="SnapshotLines"/>).
    /// </summary>
    public SelectionPanelWidget OnSnapshot(Func<string, Task> handler)
        => this with { SnapshotHandler = handler };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as SelectionPanelNode ?? new SelectionPanelNode();
        node.SnapshotHandler = SnapshotHandler;
        node.Child = await context.ReconcileChildAsync(node.Child, Child, node);
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(SelectionPanelNode);
}
