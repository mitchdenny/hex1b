using System.Text;
using Hex1b;
using Spectre.Console;

namespace Hex1b.Integrations.Spectre.SpectreConsole;

/// <summary>
/// An <see cref="IAnsiConsoleOutput"/> backed by an
/// <see cref="IHex1bAppTerminalWorkloadAdapter"/>. The Spectre console writes
/// its ANSI directly into the Hex1b workload pipeline, so it is recorded,
/// presented, and (optionally) embedded by Hex1b in the same way any other
/// workload would be.
/// </summary>
public sealed class Hex1bAnsiConsoleOutput : IAnsiConsoleOutput
{
    private readonly IHex1bAppTerminalWorkloadAdapter _adapter;
    private readonly Hex1bWorkloadTextWriter _writer;

    /// <summary>
    /// Initializes a new <see cref="Hex1bAnsiConsoleOutput"/> wrapping the
    /// supplied workload adapter.
    /// </summary>
    /// <param name="adapter">The workload adapter receiving Spectre's ANSI output.</param>
    public Hex1bAnsiConsoleOutput(IHex1bAppTerminalWorkloadAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _writer = new Hex1bWorkloadTextWriter(adapter);
    }

    /// <inheritdoc />
    public TextWriter Writer => _writer;

    /// <inheritdoc />
    /// <remarks>
    /// Always <c>true</c>: Spectre is always writing into a virtual terminal
    /// owned by Hex1b, so it should treat the sink as fully interactive
    /// (alternate buffer, cursor control, mouse — the works).
    /// </remarks>
    public bool IsTerminal => true;

    /// <inheritdoc />
    public int Width => _adapter.Width > 0 ? _adapter.Width : 80;

    /// <inheritdoc />
    public int Height => _adapter.Height > 0 ? _adapter.Height : 24;

    /// <inheritdoc />
    /// <remarks>
    /// No-op. The bridge always emits UTF-8 because Hex1b's workload pipeline
    /// is UTF-8 throughout. Honouring an arbitrary requested encoding here
    /// would risk the Spectre app emitting bytes that Hex1b's ANSI parser
    /// could not decode.
    /// </remarks>
    public void SetEncoding(Encoding encoding)
    {
        // intentional no-op
    }
}
