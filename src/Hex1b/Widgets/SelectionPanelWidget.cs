using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A container that wraps a child widget so that the user can later snapshot
/// or select over the rendered content. The eventual goal is a full copy/select
/// mode comparable to <see cref="TerminalWidget"/>'s, but today the widget only
/// supports a single proof-of-concept operation: a snapshot of the text inside
/// the wrapped subtree, surfaced via <see cref="OnSnapshot(System.Action{string})"/>.
/// </summary>
/// <remarks>
/// Layout, focus, and input handling are otherwise delegated to the child
/// unchanged. When a snapshot handler is registered, the panel installs a
/// global binding (default: <c>Ctrl+Shift+S</c>, action id
/// <see cref="Snapshot"/>) that walks the wrapped subtree, joins the text it
/// finds, and invokes the handler with the result.
/// </remarks>
/// <param name="Child">The child widget to wrap.</param>
public sealed record SelectionPanelWidget(Hex1bWidget Child) : Hex1bWidget
{
    /// <summary>Rebindable action: snapshot the wrapped content as plain text.</summary>
    public static readonly ActionId Snapshot = new($"{nameof(SelectionPanelWidget)}.{nameof(Snapshot)}");

    internal Func<string, Task>? SnapshotHandler { get; init; }

    /// <summary>
    /// Sets a synchronous snapshot handler. Invoked with the joined plain-text
    /// content of the wrapped subtree when the user triggers
    /// <see cref="Snapshot"/> (default <c>Ctrl+Shift+S</c>).
    /// </summary>
    public SelectionPanelWidget OnSnapshot(Action<string> handler)
        => this with { SnapshotHandler = text => { handler(text); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous snapshot handler. Invoked with the joined plain-text
    /// content of the wrapped subtree when the user triggers
    /// <see cref="Snapshot"/> (default <c>Ctrl+Shift+S</c>).
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
