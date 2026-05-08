using System.Text;
using Hex1b;
using Hex1b.Integrations.Spectre.SpectreConsole;

namespace Hex1b.Integrations.Spectre.Tests;

public class Hex1bAnsiConsoleOutputTests
{
    [Fact]
    public void Writer_WhenWritingString_ForwardsBytesToAdapter()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var output = new Hex1bAnsiConsoleOutput(adapter);

        output.Writer.Write("hello");
        output.Writer.Flush();

        var captured = DrainOutput(adapter);
        Assert.Equal("hello", captured);
    }

    [Fact]
    public void Writer_WhenWritingChar_ForwardsCharToAdapter()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var output = new Hex1bAnsiConsoleOutput(adapter);

        output.Writer.Write('x');
        output.Writer.Flush();

        var captured = DrainOutput(adapter);
        Assert.Equal("x", captured);
    }

    [Fact]
    public void Writer_WhenWritingAnsiSequence_PreservesEscapeBytes()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var output = new Hex1bAnsiConsoleOutput(adapter);

        output.Writer.Write("\x1b[31mred\x1b[0m");
        output.Writer.Flush();

        var captured = DrainOutput(adapter);
        Assert.Equal("\x1b[31mred\x1b[0m", captured);
    }

    [Fact]
    public void Writer_WhenWritingSpan_ForwardsContents()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var output = new Hex1bAnsiConsoleOutput(adapter);

        output.Writer.Write("abcdef".AsSpan(1, 4));
        output.Writer.Flush();

        var captured = DrainOutput(adapter);
        Assert.Equal("bcde", captured);
    }

    [Fact]
    public void IsTerminal_AlwaysReturnsTrue()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var output = new Hex1bAnsiConsoleOutput(adapter);

        Assert.True(output.IsTerminal);
    }

    [Fact]
    public void Encoding_AlwaysUtf8()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var output = new Hex1bAnsiConsoleOutput(adapter);

        Assert.Equal(Encoding.UTF8, output.Writer.Encoding);
    }

    [Fact]
    public void SetEncoding_DoesNotThrow()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var output = new Hex1bAnsiConsoleOutput(adapter);

        // Should silently no-op rather than throw.
        output.SetEncoding(Encoding.ASCII);
        Assert.Equal(Encoding.UTF8, output.Writer.Encoding);
    }

    private static string DrainOutput(Hex1bAppWorkloadAdapter adapter)
    {
        var sb = new StringBuilder();
        while (adapter.TryReadOutput(out var bytes))
        {
            sb.Append(Encoding.UTF8.GetString(bytes.Span));
        }
        return sb.ToString();
    }
}
