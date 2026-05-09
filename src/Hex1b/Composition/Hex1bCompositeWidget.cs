using System.Diagnostics.CodeAnalysis;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Composition;

/// <summary>
/// Base class for <em>composite widgets</em> — widgets that compose other widgets without
/// owning a dedicated node type. Override <see cref="Build"/> to declare the widget tree
/// in terms of state held in the supplied <see cref="CompositionContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// A composite widget never authors a custom <see cref="Hex1bNode"/>: every composite
/// reconciles into a single shared <see cref="Hex1bCompositeNode"/> whose layout, focus,
/// and rendering are delegated to whatever <see cref="Build"/> returns. The node type is
/// detected as a "match" by the reconciler regardless of the concrete composite type, so
/// switching from one composite to another at the same tree position recycles the node
/// shell while disposing prior state.
/// </para>
/// <para>
/// State is held on the composite's node and surfaced through
/// <see cref="CompositionContext.UseState{T}(System.Func{T})"/>. Values can be shared with
/// descendants via <see cref="CompositionContext.Provide{T}(T)"/> and consumed with
/// <see cref="CompositionContext.Use{T}()"/>.
/// </para>
/// <para>
/// Composite widgets follow the standard widget convention: subclasses must end in
/// <c>Widget</c>, must be declared <c>record</c>, and must not declare <c>With*</c> methods
/// (these rules are enforced by the HEX1B0001–HEX1B0005 analyzers).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed record CounterWidget(string Label) : Hex1bCompositeWidget
/// {
///     protected override Hex1bWidget Build(CompositionContext ctx)
///     {
///         var state = ctx.UseState(() => new CounterState());
///         return ctx.HStack(h => [
///             h.Text($"{Label}: {state.Count}"),
///             h.Button("+").OnClick(_ => state.Count++),
///         ]);
///     }
///
///     private sealed class CounterState
///     {
///         public int Count;
///     }
/// }
/// </code>
/// </example>
[Experimental("HEX1B_COMPOSITION", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/composition.md")]
public abstract record Hex1bCompositeWidget : Hex1bWidget
{
    /// <summary>
    /// Builds the widget tree for this composite. Called on every reconciliation pass.
    /// </summary>
    /// <param name="ctx">
    /// The composition context that exposes per-instance state via
    /// <see cref="CompositionContext.UseState{T}(System.Func{T})"/> and ambient values via
    /// <see cref="CompositionContext.Provide{T}(T)"/> and <see cref="CompositionContext.Use{T}()"/>.
    /// </param>
    /// <returns>The widget tree to render. Must not be <c>null</c>.</returns>
    /// <remarks>
    /// <para>
    /// Build is invoked synchronously; for asynchronous work, use the standard
    /// <see cref="CompositionContext.UseState{T}(System.Func{T})"/> + state-mutation pattern
    /// (kick off the work the first time, mutate state, call <c>app.Invalidate()</c> when
    /// done). The <see cref="CompositionContext.CancellationToken"/> can be observed for
    /// cooperative cancellation.
    /// </para>
    /// <para>
    /// Build runs every frame, so it should be cheap. Heavy work belongs in state objects
    /// that are computed once and read on subsequent frames.
    /// </para>
    /// </remarks>
    protected abstract Hex1bWidget Build(CompositionContext ctx);

    internal sealed override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as Hex1bCompositeNode;

        // If this node was previously owned by a different composite type, dispose its
        // state and start over so we don't leak references between unrelated composites.
        if (node is not null && node.CompositeWidgetType != GetType())
        {
            node.DisposeAllState();
            node = null;
        }

        var isNew = node is null;
        node ??= new Hex1bCompositeNode();
        node.CompositeWidgetType = GetType();

        var compositionContext = new CompositionContext(node, context);
        var built = Build(compositionContext);
        if (built is null)
        {
            throw new InvalidOperationException(
                $"Hex1bCompositeWidget.Build returned null on '{GetType().FullName}'. " +
                "Composites must always return a widget tree to render.");
        }

        node.Child = await context.ReconcileChildAsync(node.Child, built, node);

        if (isNew)
            node.MarkDirty();

        return node;
    }

    internal sealed override Type GetExpectedNodeType() => typeof(Hex1bCompositeNode);
}
