using Hex1b.Composition;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Marks a widget as supporting an externally-supplied state object of type
/// <typeparamref name="TState"/>. Implementing widgets expose a fluent
/// <c>State(TState)</c> method that returns a copy of the widget bound to the
/// supplied state instance.
/// </summary>
/// <typeparam name="TSelf">
/// The implementing widget type. Self-bound (F-bounded) so <see cref="State(TState)"/>
/// returns the concrete widget type, preserving fluent chaining.
/// </typeparam>
/// <typeparam name="TState">
/// The reference-type state object the widget can be backed by. Constrained to
/// <c>class</c> so the parent and the widget node share the same instance and
/// observe each other's mutations.
/// </typeparam>
/// <remarks>
/// <para>
/// This interface is the framework-wide convention for "lifting a widget's state
/// out of its node". The intended pairing is with
/// <see cref="CompositionContext.UseState{T}(System.Func{T})"/> inside a composite's
/// <see cref="Hex1bWidget.Build(CompositionContext)"/> override:
/// </para>
/// <code>
/// var state = ctx.UseState(() => new TextBoxState());
/// return ctx.TextBox().State(state);
/// </code>
/// <para>
/// Once the parent owns the state, the widget becomes a pure view of it: the
/// parent can mutate <c>state.X = …</c> between renders and those mutations are
/// reflected on the next reconcile, without any <c>OnXChanged</c> shadow-syncing.
/// </para>
/// <para>
/// Implementations should:
/// </para>
/// <list type="bullet">
/// <item>store the supplied state in an internal <c>InjectedState</c> init-only
///   property and route it into the underlying node during <c>ReconcileAsync</c>;</item>
/// <item>throw <see cref="System.InvalidOperationException"/> if the widget also
///   carries conflicting initial-value parameters supplied via its primary
///   constructor (so misuse fails fast rather than silently picking one);</item>
/// <item>name the fluent method exactly <c>State</c> — the analyzer
///   <c>HEX1B0001</c> forbids the <c>With*</c> prefix on widget extension and
///   instance methods.</item>
/// </list>
/// </remarks>
public interface IStatefulWidget<TSelf, TState>
    where TSelf : Hex1bWidget, IStatefulWidget<TSelf, TState>
    where TState : class
{
    /// <summary>
    /// Returns a copy of the widget bound to the supplied <paramref name="state"/>
    /// instance. The framework will route this exact instance into the underlying
    /// node on every reconcile, making the widget a pure view of <paramref name="state"/>.
    /// </summary>
    /// <param name="state">The state object owned by the calling composite.</param>
    TSelf State(TState state);
}
