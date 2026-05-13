namespace Hex1b;

/// <summary>
/// Optional capability marker for <see cref="IHex1bTerminalWorkloadAdapter"/> implementations
/// that maintain an internal "last frame painted" diff cache and can be asked
/// to drop it so the next frame is emitted in full.
/// </summary>
/// <remarks>
/// <para>
/// Implemented by <see cref="Hex1bAppWorkloadAdapter"/>: when the surrounding terminal's
/// screen state has been reset out from under the app (e.g. by
/// <see cref="PlaceholderWorkloadAdapter"/> swapping workloads and synthesising a
/// reset sequence), the app's diff cache no longer reflects the actual on-screen state.
/// Calling <see cref="RequestFullRepaint"/> tells the app to behave as if the next
/// frame is the first frame.
/// </para>
/// <para>
/// Workloads whose output is fully replayed by the producer on (re)connect — HMP1's
/// StateSync frame is the canonical case — do not need this capability.
/// </para>
/// </remarks>
public interface IRepaintableWorkloadAdapter
{
    /// <summary>
    /// Discards any cached diff state and requests that the workload's next frame
    /// be emitted as a full screen repaint.
    /// </summary>
    /// <remarks>
    /// Safe to call from any thread. May invalidate the workload immediately so the
    /// repaint actually happens without waiting for the next external state change.
    /// </remarks>
    void RequestFullRepaint();
}
