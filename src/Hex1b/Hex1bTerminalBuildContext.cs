using System.Diagnostics;
using System.Net.WebSockets;
using Hex1b.Automation;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Context returned by workload factories during terminal build.
/// </summary>
internal sealed class Hex1bTerminalBuildContext(
    IHex1bTerminalWorkloadAdapter workloadAdapter,
    Func<CancellationToken, Task<int>>? runCallback)
{
    /// <summary>
    /// The workload adapter to use.
    /// </summary>
    public IHex1bTerminalWorkloadAdapter WorkloadAdapter { get; } = workloadAdapter;

    /// <summary>
    /// Optional callback that runs the workload and returns an exit code.
    /// If null, the terminal will wait for the workload to disconnect.
    /// </summary>
    public Func<CancellationToken, Task<int>>? RunCallback { get; } = runCallback;
}
