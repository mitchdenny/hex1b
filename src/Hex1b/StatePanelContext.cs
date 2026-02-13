using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// A context for building the child tree of a <see cref="StatePanelWidget"/>.
/// Provides access to generic state storage scoped to the panel's identity.
/// </summary>
/// <remarks>
/// <para>
/// This context is created during reconciliation (not during widget building),
/// so the underlying node has already been resolved via identity matching.
/// On first render, a new node is created.
/// </para>
/// <para>
/// Subsystems (e.g. animation) can store their state via <see cref="GetState{T}"/>.
/// Re-render scheduling is automatic via the <see cref="IActiveState"/> interface.
/// </para>
/// </remarks>
public sealed class StatePanelContext : WidgetContext<StatePanelWidget>
{
    private readonly StatePanelNode _node;

    internal StatePanelContext(StatePanelNode node, TimeSpan elapsed)
    {
        _node = node;
        Elapsed = elapsed;
    }

    /// <summary>
    /// Time elapsed since the last reconciliation frame for this panel.
    /// Subsystems can use this to advance time-based state (e.g. animations).
    /// </summary>
    public TimeSpan Elapsed { get; }

    /// <summary>
    /// Gets or creates a state object of the specified type. The factory is called once
    /// on first access; subsequent calls return the same instance. State persists
    /// across reconciliation frames for the same state key.
    /// </summary>
    /// <typeparam name="T">The state type. Must be a reference type.</typeparam>
    /// <param name="factory">Factory invoked on first access to create the state object.</param>
    public T GetState<T>(Func<T> factory) where T : class
        => _node.GetState(factory);
}
