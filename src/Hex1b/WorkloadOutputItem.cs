using Hex1b.Tokens;

namespace Hex1b;

/// <summary>
/// Represents a chunk of output read from a workload.
/// </summary>
/// <remarks>
/// <para>
/// Workloads normally expose output as raw UTF-8 bytes via
/// <see cref="IHex1bTerminalWorkloadAdapter.ReadOutputAsync"/>, which the terminal then
/// decodes and tokenizes.
/// </para>
/// <para>
/// Some workloads can also provide a pre-tokenized representation of that same output.
/// When <see cref="Tokens"/> is provided, <see cref="Hex1bTerminal"/> can skip UTF-8 decode and
/// <see cref="AnsiTokenizer"/> tokenization, reducing allocations and CPU.
/// </para>
/// </remarks>
/// <param name="Bytes">The raw UTF-8 output bytes from the workload.</param>
/// <param name="Tokens">
/// Optional pre-tokenized representation of <paramref name="Bytes"/>. When provided, it should
/// represent the same output as <paramref name="Bytes"/>.
/// </param>
public readonly record struct WorkloadOutputItem(
    ReadOnlyMemory<byte> Bytes,
    IReadOnlyList<AnsiToken>? Tokens)
{
    /// <summary>
    /// Optional pooled array backing <see cref="Bytes"/>. When non-null, the terminal returns
    /// it to <see cref="System.Buffers.ArrayPool{T}.Shared"/> after the bytes have been fully
    /// forwarded. This is an internal mechanism used by the in-process Hex1bApp ↔ Hex1bTerminal
    /// bridge to avoid per-frame LOH allocations for large ANSI output buffers (a busy
    /// fullscreen frame is well over the 85KB LOH threshold). External workload adapters
    /// should not set this — they should manage any pooling internally and present
    /// <see cref="Bytes"/> as a stable window valid until their next read call.
    /// </summary>
    internal byte[]? PooledBuffer { get; init; }

    /// <summary>
    /// Optional pooled token list backing <see cref="Tokens"/>. When non-null, the terminal
    /// returns it to the originating <see cref="Hex1bApp"/> via
    /// <see cref="Hex1bApp.ReturnTokenList"/> after consumption. Same purpose and constraints
    /// as <see cref="PooledBuffer"/>: this is an internal in-process mechanism and external
    /// workload adapters should leave it null.
    /// </summary>
    internal List<AnsiToken>? PooledTokens { get; init; }

    /// <summary>
    /// Internal callback the consumer invokes to return <see cref="PooledTokens"/> to its
    /// owning pool. Decoupled from <see cref="Hex1bApp"/> to avoid a circular type reference.
    /// </summary>
    internal Action<List<AnsiToken>>? PooledTokensReturn { get; init; }
}

