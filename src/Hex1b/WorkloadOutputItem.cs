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
public readonly record struct WorkloadOutputItem(ReadOnlyMemory<byte> Bytes, IReadOnlyList<AnsiToken>? Tokens);

