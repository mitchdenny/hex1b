namespace Hex1b;

/// <summary>
/// Interface for state objects stored in a <see cref="Hex1b.Nodes.StatePanelNode"/> that
/// participate in the per-frame lifecycle. The reconciliation system calls
/// <see cref="OnFrameAdvance"/> once per frame before the builder runs, and checks
/// <see cref="IsActive"/> afterward to determine if re-rendering should continue.
/// </summary>
public interface IActiveState
{
    /// <summary>
    /// Called once per reconciliation frame with the time elapsed since the previous frame.
    /// Use this to advance time-based state (e.g. animations).
    /// </summary>
    void OnFrameAdvance(TimeSpan elapsed);

    /// <summary>
    /// Returns true if this state requires continued re-rendering (e.g. running animations).
    /// </summary>
    bool IsActive { get; }
}
