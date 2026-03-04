using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A container widget that intercepts bracketed paste events for all descendants.
/// When paste data arrives and no child handles it, the <see cref="PasteHandler"/> receives the 
/// <see cref="PasteContext"/> for streaming processing.
/// </summary>
/// <example>
/// <code>
/// ctx.Pastable(
///     child: ctx.VStack(v => [
///         v.Text("Drop zone"),
///         v.TextBlock($"Received: {bytesReceived} bytes"),
///     ]),
///     onPaste: async paste =>
///     {
///         await foreach (var chunk in paste.ReadChunksAsync())
///         {
///             bytesReceived += chunk.Length;
///         }
///     }
/// )
/// </code>
/// </example>
public sealed record PastableWidget(Hex1bWidget Child) : Hex1bWidget
{
    /// <summary>
    /// The async paste handler. Called when a bracketed paste event bubbles up to this container.
    /// </summary>
    internal Func<PasteContext, Task>? PasteHandler { get; init; }

    /// <summary>
    /// Maximum number of characters to accept. If exceeded, the paste is auto-cancelled.
    /// Null means unlimited.
    /// </summary>
    internal int? MaxSize { get; init; }

    /// <summary>
    /// Maximum duration for the paste operation. If exceeded, the paste is auto-cancelled.
    /// Null means no timeout.
    /// </summary>
    internal TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Sets the async paste handler that receives streaming paste data.
    /// </summary>
    public PastableWidget OnPaste(Func<PasteContext, Task> handler)
        => this with { PasteHandler = handler };

    /// <summary>
    /// Sets the async paste handler using a synchronous action.
    /// </summary>
    public PastableWidget OnPaste(Action<PasteContext> handler)
        => this with { PasteHandler = paste => { handler(paste); return Task.CompletedTask; } };

    /// <summary>
    /// Sets the maximum paste size in characters. If exceeded, the paste is auto-cancelled.
    /// </summary>
    public PastableWidget WithMaxSize(int maxCharacters)
        => this with { MaxSize = maxCharacters };

    /// <summary>
    /// Sets a timeout for the paste operation. If exceeded, the paste is auto-cancelled.
    /// </summary>
    public PastableWidget WithTimeout(TimeSpan timeout)
        => this with { Timeout = timeout };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as PastableNode ?? new PastableNode();
        node.Child = await context.ReconcileChildAsync(node.Child, Child, node);
        node.PasteAction = PasteHandler;
        node.MaxSize = MaxSize;
        node.PasteTimeout = Timeout;
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(PastableNode);
}
