using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A container that wraps a child widget and adds a TerminalWidget-style
/// copy mode for selecting and copying regions of the rendered content.
/// </summary>
/// <remarks>
/// <para>
/// SelectionPanel is otherwise transparent: layout, focus, and input flow
/// through to the child unchanged. Copy mode is a modal interaction
/// triggered by <see cref="EnterCopyMode"/> (default <c>F12</c>). While
/// active, the panel:
/// </para>
/// <list type="bullet">
/// <item>Captures all subsequent input (focus on outer widgets is
///       preserved but their bindings are bypassed).</item>
/// <item>Renders an inverted-cell cursor over the wrapped content.</item>
/// <item>Listens for movement keys (<c>arrows</c>/<c>hjkl</c>,
///       <c>w</c>/<c>b</c>, <c>PageUp</c>/<c>PageDown</c>,
///       <c>Home</c>/<c>End</c>, <c>g</c>/<c>G</c>) and selection-mode
///       toggles (<c>v</c> character, <c>V</c> line, <c>Alt+v</c>
///       block).</item>
/// <item>Copies the selection on <c>y</c>/<c>Enter</c> via
///       <see cref="OnCopy(System.Action{string})"/> and exits.</item>
/// <item>Cancels with <c>Esc</c> or <c>q</c> without copying.</item>
/// </list>
/// <para>
/// Cursor coordinates are in surface-local space — the column/row of the
/// cell as it appears in the rendered child. The cursor starts at the
/// bottom-left when copy mode is entered, which matches chat-style
/// interfaces where the most recent content sits at the bottom of an
/// auto-scrolling viewport. Selection geometry mirrors
/// <see cref="TerminalWidget"/>'s three modes:
/// </para>
/// <list type="bullet">
/// <item><see cref="SelectionMode.Character"/> — terminal-style cell
///       stream from anchor to cursor, wrapping across rows.</item>
/// <item><see cref="SelectionMode.Line"/> — full rows from anchor row to
///       cursor row.</item>
/// <item><see cref="SelectionMode.Block"/> — rectangle defined by the
///       anchor and cursor positions.</item>
/// </list>
/// <para>
/// All key bindings are rebindable via <c>WithInputBindings</c>
/// using the published <c>ActionId</c> constants on this widget. The
/// default keys mirror <see cref="TerminalWidget"/>'s
/// <see cref="CopyModeBindingsOptions"/> defaults so users familiar with
/// vi-style terminal copy mode get the same muscle memory. The entry
/// key is <c>F12</c> (rather than <c>F6</c>) to avoid colliding with
/// <see cref="TerminalWidget"/>'s <see cref="TerminalWidget.EnterCopyMode"/>
/// default in apps that contain both widgets, and to dodge Windows
/// Terminal's <c>F11</c> full-screen toggle.
/// </para>
/// </remarks>
/// <param name="Child">The child widget to wrap.</param>
public sealed record SelectionPanelWidget(Hex1bWidget Child) : Hex1bWidget
{
    // Entry --------------------------------------------------------------

    /// <summary>Rebindable action: enter copy mode. Default: <c>F12</c>.</summary>
    public static readonly ActionId EnterCopyMode = new($"{nameof(SelectionPanelWidget)}.{nameof(EnterCopyMode)}");

    // Movement (capture-override; only fire while copy mode is active) ---

    /// <summary>Rebindable action: move the copy mode cursor up one row. Default: <c>UpArrow</c>, <c>K</c>.</summary>
    public static readonly ActionId CopyModeUp = new($"{nameof(SelectionPanelWidget)}.{nameof(CopyModeUp)}");
    /// <summary>Rebindable action: move the copy mode cursor down one row. Default: <c>DownArrow</c>, <c>J</c>.</summary>
    public static readonly ActionId CopyModeDown = new($"{nameof(SelectionPanelWidget)}.{nameof(CopyModeDown)}");
    /// <summary>Rebindable action: move the copy mode cursor left one column. Default: <c>LeftArrow</c>, <c>H</c>.</summary>
    public static readonly ActionId CopyModeLeft = new($"{nameof(SelectionPanelWidget)}.{nameof(CopyModeLeft)}");
    /// <summary>Rebindable action: move the copy mode cursor right one column. Default: <c>RightArrow</c>, <c>L</c>.</summary>
    public static readonly ActionId CopyModeRight = new($"{nameof(SelectionPanelWidget)}.{nameof(CopyModeRight)}");
    /// <summary>Rebindable action: move the cursor forward one word. Default: <c>W</c>.</summary>
    public static readonly ActionId CopyModeWordForward = new($"{nameof(SelectionPanelWidget)}.{nameof(CopyModeWordForward)}");
    /// <summary>Rebindable action: move the cursor backward one word. Default: <c>B</c>.</summary>
    public static readonly ActionId CopyModeWordBackward = new($"{nameof(SelectionPanelWidget)}.{nameof(CopyModeWordBackward)}");
    /// <summary>Rebindable action: move the cursor up one page. Default: <c>PageUp</c>.</summary>
    public static readonly ActionId CopyModePageUp = new($"{nameof(SelectionPanelWidget)}.{nameof(CopyModePageUp)}");
    /// <summary>Rebindable action: move the cursor down one page. Default: <c>PageDown</c>.</summary>
    public static readonly ActionId CopyModePageDown = new($"{nameof(SelectionPanelWidget)}.{nameof(CopyModePageDown)}");
    /// <summary>Rebindable action: move the cursor to the start of the current row. Default: <c>Home</c>, <c>D0</c>.</summary>
    public static readonly ActionId CopyModeLineStart = new($"{nameof(SelectionPanelWidget)}.{nameof(CopyModeLineStart)}");
    /// <summary>Rebindable action: move the cursor to the end of the current row. Default: <c>End</c>.</summary>
    public static readonly ActionId CopyModeLineEnd = new($"{nameof(SelectionPanelWidget)}.{nameof(CopyModeLineEnd)}");
    /// <summary>Rebindable action: move the cursor to the top of the buffer. Default: <c>G</c>.</summary>
    public static readonly ActionId CopyModeBufferTop = new($"{nameof(SelectionPanelWidget)}.{nameof(CopyModeBufferTop)}");
    /// <summary>Rebindable action: move the cursor to the bottom of the buffer. Default: <c>Shift+G</c>.</summary>
    public static readonly ActionId CopyModeBufferBottom = new($"{nameof(SelectionPanelWidget)}.{nameof(CopyModeBufferBottom)}");

    // Selection mode toggles ---------------------------------------------

    /// <summary>Rebindable action: start or toggle a character (cell-stream) selection. Default: <c>V</c>, <c>Spacebar</c>.</summary>
    public static readonly ActionId CopyModeStartSelection = new($"{nameof(SelectionPanelWidget)}.{nameof(CopyModeStartSelection)}");
    /// <summary>Rebindable action: start or toggle a line selection. Default: <c>Shift+V</c>.</summary>
    public static readonly ActionId CopyModeToggleLineMode = new($"{nameof(SelectionPanelWidget)}.{nameof(CopyModeToggleLineMode)}");
    /// <summary>Rebindable action: start or toggle a block (rectangular) selection. Default: <c>Alt+V</c>.</summary>
    public static readonly ActionId CopyModeToggleBlockMode = new($"{nameof(SelectionPanelWidget)}.{nameof(CopyModeToggleBlockMode)}");

    // Actions ------------------------------------------------------------

    /// <summary>Rebindable action: copy the current selection and exit copy mode. Default: <c>Y</c>, <c>Enter</c>.</summary>
    public static readonly ActionId CopyModeCopy = new($"{nameof(SelectionPanelWidget)}.{nameof(CopyModeCopy)}");
    /// <summary>Rebindable action: cancel copy mode without copying. Default: <c>Escape</c>, <c>Q</c>.</summary>
    public static readonly ActionId CopyModeCancel = new($"{nameof(SelectionPanelWidget)}.{nameof(CopyModeCancel)}");

    internal Func<string, Task>? CopyHandler { get; init; }

    /// <summary>
    /// Sets a synchronous copy handler. Invoked with the plain text of the
    /// current selection when the user commits via <see cref="CopyModeCopy"/>.
    /// </summary>
    public SelectionPanelWidget OnCopy(Action<string> handler)
        => this with { CopyHandler = text => { handler(text); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous copy handler. Invoked with the plain text of
    /// the current selection when the user commits via <see cref="CopyModeCopy"/>.
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
