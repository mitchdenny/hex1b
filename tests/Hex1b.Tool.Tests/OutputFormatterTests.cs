using Hex1b.Tool.Infrastructure;

namespace Hex1b.Tool.Tests;

public class OutputFormatterTests
{
    [Fact]
    public void WriteTable_CalculatesColumnWidths()
    {
        var formatter = new OutputFormatter();
        var output = CaptureConsoleOutput(() =>
        {
            formatter.WriteTable(
                ["ID", "NAME", "STATUS"],
                [
                    ["1", "short", "ok"],
                    ["2", "a-longer-name", "running"]
                ]);
        });

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length); // header + 2 rows

        // Header columns should be padded to widest value
        Assert.Contains("NAME", lines[0]);
        Assert.Contains("a-longer-name", lines[2]);

        // Values should be aligned with header
        var nameStartInHeader = lines[0].IndexOf("NAME", StringComparison.Ordinal);
        var nameStartInRow = lines[2].IndexOf("a-longer-name", StringComparison.Ordinal);
        Assert.Equal(nameStartInHeader, nameStartInRow);
    }

    [Fact]
    public void WriteTable_JsonMode_ProducesNoOutput()
    {
        var formatter = new OutputFormatter { JsonMode = true };
        var output = CaptureConsoleOutput(() =>
        {
            formatter.WriteTable(
                ["ID"],
                [["1"]]);
        });

        Assert.Empty(output.Trim());
    }

    [Fact]
    public void WriteJson_ProducesFormattedJson()
    {
        var formatter = new OutputFormatter { JsonMode = true };
        var output = CaptureConsoleOutput(() =>
        {
            formatter.WriteJson(new { name = "test", count = 42 });
        });

        Assert.Contains("\"name\": \"test\"", output);
        Assert.Contains("\"count\": 42", output);
    }

    [Fact]
    public void WriteLine_NormalMode_WritesOutput()
    {
        var formatter = new OutputFormatter();
        var output = CaptureConsoleOutput(() =>
        {
            formatter.WriteLine("hello world");
        });

        Assert.Equal("hello world", output.Trim());
    }

    [Fact]
    public void WriteLine_JsonMode_SuppressesOutput()
    {
        var formatter = new OutputFormatter { JsonMode = true };
        var output = CaptureConsoleOutput(() =>
        {
            formatter.WriteLine("hello world");
        });

        Assert.Empty(output.Trim());
    }

    [Fact]
    public void WriteError_AlwaysWritesToStdErr()
    {
        var formatter = new OutputFormatter { JsonMode = true };
        var errorOutput = CaptureConsoleError(() =>
        {
            formatter.WriteError("something went wrong");
        });

        Assert.Equal("something went wrong", errorOutput.Trim());
    }

    [Fact]
    public void WriteTable_EmptyRows_WritesHeaderOnly()
    {
        var formatter = new OutputFormatter();
        var output = CaptureConsoleOutput(() =>
        {
            formatter.WriteTable(["COL1", "COL2"], []);
        });

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.Contains("COL1", lines[0]);
    }

    [Fact]
    public void WriteTable_ColumnsAligned_WithTwoSpaceSeparator()
    {
        var formatter = new OutputFormatter();
        var output = CaptureConsoleOutput(() =>
        {
            formatter.WriteTable(
                ["A", "B"],
                [["xx", "yy"]]);
        });

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // "A " + "  " + "B " pattern — two-space separator
        Assert.Contains("  ", lines[0]);
    }

    private static string CaptureConsoleOutput(Action action)
    {
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static string CaptureConsoleError(Action action)
    {
        var originalErr = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);
        try
        {
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetError(originalErr);
        }
    }
}
