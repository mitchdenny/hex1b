using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A container that wraps a child widget so the user can later select and
/// copy regions of the rendered content. The eventual goal is a full
/// copy/select mode comparable to <see cref="TerminalWidget"/>'s; today
/// the widget supports a two-stage proof-of-concept copy:
/// <list type="number">
/// <item>Press a selection key (default <c>F7</c>/<c>F8</c>/<c>F9</c>) to
///       enter a preview for one of the three selection geometries
///       (cells / block / lines). The selected cells are visually inverted
///       in place so the user can see what will be copied.</item>
/// <item>Press the copy key (default <c>F12</c>) to commit: the
///       <see cref="OnCopy(System.Action{string})"/> handler is invoked
///       with the plain text of the previewed selection and the preview
///       is cleared. If no preview is active, <c>F12</c> copies the
///       entire content (same as the original snapshot behaviour).</item>
/// </list>
/// </summary>
/// <remarks>
/// Layout, focus, and input handling are otherwise delegated to the child
/// unchanged. When a copy handler is registered, the panel installs global
/// bindings for each selection mode plus the copy action: <c>F7</c>
/// <see cref="SelectCells"/>, <c>F8</c> <see cref="SelectBlock"/>, <c>F9</c>
/// <see cref="SelectLines"/>, <c>F12</c> <see cref="Copy"/>. Function keys
/// are used by default because terminals reliably surface F-key events
/// (with optional modifiers) — letter keys with <c>Ctrl+Shift</c> are
/// folded to an ASCII control byte by Windows console and most xterm-style
/// terminals, so a chord like <c>Ctrl+Shift+S</c> is not distinguishable
/// from <c>Ctrl+S</c>. The defaults also dodge two well-known F-key
/// conflicts: <c>F6</c> is the <see cref="TerminalWidget"/> copy-mode
/// default, and <c>F11</c> toggles full screen in Windows Terminal
/// (intercepted before the app sees it).
/// </remarks>
/// <param name="Child">The child widget to wrap.</param>
public sealed record SelectionPanelWidget(Hex1bWidget Child) : Hex1bWidget
{
    /// <summary>Rebindable action: copy the currently previewed selection (or the entire content if no preview is active). Default: <c>F12</c>.</summary>
    public static readonly ActionId Copy = new($"{nameof(SelectionPanelWidget)}.{nameof(Copy)}");

    /// <summary>Rebindable action: enter the cell-stream (terminal-style) selection preview. Default: <c>F7</c>.</summary>
    public static readonly ActionId SelectCells = new($"{nameof(SelectionPanelWidget)}.{nameof(SelectCells)}");

    /// <summary>Rebindable action: enter the rectangular block selection preview. Default: <c>F8</c>.</summary>
    public static readonly ActionId SelectBlock = new($"{nameof(SelectionPanelWidget)}.{nameof(SelectBlock)}");

    /// <summary>Rebindable action: enter the whole-line selection preview. Default: <c>F9</c>.</summary>
    public static readonly ActionId SelectLines = new($"{nameof(SelectionPanelWidget)}.{nameof(SelectLines)}");

    internal Func<string, Task>? CopyHandler { get; init; }

    /// <summary>
    /// Sets a synchronous copy handler. Invoked with the plain-text content
    /// of the current selection (or the entire content if no selection is
    /// active) when <see cref="Copy"/> fires.
    /// </summary>
    public SelectionPanelWidget OnCopy(Action<string> handler)
        => this with { CopyHandler = text => { handler(text); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous copy handler. Invoked with the plain-text content
    /// of the current selection (or the entire content if no selection is
    /// active) when <see cref="Copy"/> fires.
    /// </summary>
    public SelectionPanelWidget OnCopy(Func<string, Task> handler)
        => this with { CopyHandler = handler };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as SelectionPanelNode ?? new SelectionPanelNode();
        node.CopyHandler = CopyHandler;
        node.Child = await context.ReconcileChildAsync(node.Child, Child, node);
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(SelectionPanelNode);
}
