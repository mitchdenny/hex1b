using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A container widget that overrides input bindings for all descendant widgets
/// of specified types. Overrides cascade through the widget tree — any widget of
/// a matching type within the subtree will have the override applied after its
/// default bindings and any per-instance <see cref="Hex1bWidget.BindingsConfigurator"/>.
/// </summary>
/// <example>
/// <code>
/// ctx.InputOverride(
///     ctx.VStack([list1, list2, textbox1])
/// )
/// .Override&lt;ListWidget&gt;(b =&gt;
/// {
///     b.Remove(ListWidget.MoveUp);
///     b.Key(Hex1bKey.K).Triggers(ListWidget.MoveUp);
/// })
/// .Override&lt;TextBoxWidget&gt;(b =&gt;
/// {
///     b.Remove(TextBoxWidget.MoveUp);
///     b.Key(Hex1bKey.K).Ctrl().Triggers(TextBoxWidget.MoveUp);
/// });
/// </code>
/// </example>
public sealed record InputOverrideWidget(Hex1bWidget Content) : Hex1bWidget
{
    /// <summary>
    /// Per-widget-type binding overrides. The key is the widget type, and the
    /// value is the configurator that modifies the <see cref="InputBindingsBuilder"/>
    /// after the widget's default and per-instance bindings have been applied.
    /// </summary>
    internal IReadOnlyDictionary<Type, Action<InputBindingsBuilder>> Overrides { get; init; }
        = new Dictionary<Type, Action<InputBindingsBuilder>>();

    /// <summary>
    /// Adds or replaces a binding override for all descendant widgets of type
    /// <typeparamref name="TWidget"/>. The <paramref name="configure"/> callback
    /// runs after the widget's default bindings and any per-instance
    /// <c>WithInputBindings</c> configurator.
    /// </summary>
    /// <typeparam name="TWidget">The widget type to override bindings for.</typeparam>
    /// <param name="configure">
    /// A callback that receives the <see cref="InputBindingsBuilder"/> and can
    /// add, remove, or remap bindings using <see cref="InputBindingsBuilder.Remove(ActionId)"/>,
    /// <see cref="InputBindingsBuilder.RemoveAll"/>, and the <c>Triggers</c> methods.
    /// </param>
    /// <returns>A new <see cref="InputOverrideWidget"/> with the override added.</returns>
    public InputOverrideWidget Override<TWidget>(Action<InputBindingsBuilder> configure)
        where TWidget : Hex1bWidget
    {
        var newOverrides = new Dictionary<Type, Action<InputBindingsBuilder>>(Overrides)
        {
            [typeof(TWidget)] = configure
        };
        return this with { Overrides = newOverrides };
    }

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as InputOverrideNode ?? new InputOverrideNode();

        // Merge our overrides with any parent overrides (inner wins)
        var mergedOverrides = context.MergeInputOverrides(Overrides);

        // Create a child context that carries the overrides down to all descendants
        var childContext = context.WithInputOverrides(mergedOverrides);

        node.Child = await childContext.ReconcileChildAsync(node.Child, Content, node);

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(InputOverrideNode);
}
