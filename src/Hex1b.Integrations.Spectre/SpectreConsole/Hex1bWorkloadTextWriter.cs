using System.Text;
using Hex1b;

namespace Hex1b.Integrations.Spectre.SpectreConsole;

/// <summary>
/// A <see cref="TextWriter"/> that forwards all writes into an
/// <see cref="IHex1bAppTerminalWorkloadAdapter"/> as if the writer were the
/// process's standard output.
/// </summary>
/// <remarks>
/// <para>
/// Spectre.Console emits ANSI escape sequences directly to its
/// <c>IAnsiConsoleOutput.Writer</c>. Wrapping a Hex1b workload adapter
/// behind a <see cref="TextWriter"/> lets the entire Spectre rendering pipeline
/// flow into Hex1b's terminal as if it were any other process producing ANSI
/// — meaning recording, presentation, and embedding all "just work".
/// </para>
/// <para>
/// All overrides delegate into the adapter via UTF-8 encoded byte writes.
/// Spectre's <c>AnsiBuilder</c> typically calls <see cref="Write(string?)"/>
/// once per render frame, so single-character writes are rare.
/// </para>
/// </remarks>
internal sealed class Hex1bWorkloadTextWriter : TextWriter
{
    private readonly IHex1bAppTerminalWorkloadAdapter _adapter;

    public Hex1bWorkloadTextWriter(IHex1bAppTerminalWorkloadAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        _adapter.Write(value.ToString());
    }

    public override void Write(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        _adapter.Write(value);
    }

    public override void Write(char[] buffer, int index, int count)
    {
        if (count == 0)
        {
            return;
        }

        _adapter.Write(new string(buffer, index, count));
    }

    public override void Write(ReadOnlySpan<char> buffer)
    {
        if (buffer.IsEmpty)
        {
            return;
        }

        _adapter.Write(buffer.ToString());
    }

    public override void WriteLine()
    {
        _adapter.Write("\n");
    }

    public override void WriteLine(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _adapter.Write(value);
        }

        _adapter.Write("\n");
    }

    public override void Flush()
    {
        _adapter.Flush();
    }
}
