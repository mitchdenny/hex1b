namespace Hex1b;

/// <summary>
/// Interface for state objects stored in a <see cref="Hex1b.Nodes.StatePanelNode"/> that
/// need continuous re-renders while active. The reconciliation system checks all stored
/// state after the builder completes and schedules a timer callback if any state is active.
/// </summary>
public interface IActiveState
{
    /// <summary>
    /// Returns true if this state requires continued re-rendering (e.g. running animations).
    /// </summary>
    bool IsActive { get; }
}
