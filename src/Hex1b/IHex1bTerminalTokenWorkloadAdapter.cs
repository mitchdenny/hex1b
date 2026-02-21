using Hex1b.Tokens;

namespace Hex1b;

/// <summary>
/// Optional extension interface for workloads that can supply pre-tokenized output.
/// </summary>
/// <remarks>
/// <para>
/// Most workloads expose output as raw UTF-8 bytes via <see cref="IHex1bTerminalWorkloadAdapter.ReadOutputAsync"/>.
/// Implement this interface when the workload already has an ANSI token stream available (or is willing to
/// tokenize once) so <see cref="Hex1bTerminal"/> can skip UTF-8 decoding and <see cref="AnsiTokenizer"/> tokenization.
/// </para>
/// <para>
/// When <see cref="WorkloadOutputItem.Tokens"/> is non-null, it should represent the same output as
/// <see cref="WorkloadOutputItem.Bytes"/>.
/// </para>
/// </remarks>
public interface IHex1bTerminalTokenWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    /// <summary>
    /// Read output FROM the workload, optionally including a pre-tokenized representation.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A workload output item containing bytes and optional tokens.</returns>
    ValueTask<WorkloadOutputItem> ReadOutputItemAsync(CancellationToken ct = default);
}

