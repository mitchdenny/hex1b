#pragma warning disable HEX1B002 // PlaceholderResumePolicy is experimental — internal usage is allowed.
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Hex1b;

/// <summary>
/// Controls what happens when the primary workload disconnects after having
/// been active.
/// </summary>
[Experimental("HEX1B002", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/placeholder.md")]
public enum PlaceholderResumePolicy
{
    /// <summary>
    /// Swap back to the placeholder workload and keep the terminal alive.
    /// Suits scenarios where the primary may reconnect (e.g. an HMP1 producer
    /// is restarted, or a UDS path is re-bound).
    /// </summary>
    OnDisconnect = 0,

    /// <summary>
    /// Treat primary disconnect as terminal disconnect — surface
    /// <see cref="IHex1bTerminalWorkloadAdapter.Disconnected"/> upstream and
    /// stop. Matches the pre-placeholder behaviour of bare HMP1 / PTY workloads.
    /// </summary>
    OneShot = 1,
}
