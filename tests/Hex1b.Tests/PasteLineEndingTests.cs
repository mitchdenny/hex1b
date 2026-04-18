using Hex1b.Input;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for PasteContext.ReadLinesAsync line-ending handling.
/// Verifies that \n, \r\n, and \r are all recognized as line separators.
/// Inspired by psmux's paste line-ending normalization tests.
/// </summary>
public class PasteLineEndingTests
{
    private static async Task<List<string>> ReadPasteLinesAsync(string pasteContent)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        var tokens = new AnsiToken[]
        {
            new SpecialKeyToken(200),
            new TextToken(pasteContent),
            new SpecialKeyToken(201),
        };

        var method = typeof(Hex1bTerminal).GetMethod(
            "DispatchTokensAsEventsAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        await (Task)method!.Invoke(terminal, [tokens, workload, CancellationToken.None])!;

        var events = new List<Hex1bEvent>();
        while (workload.InputEvents.TryRead(out var evt))
            events.Add(evt);

        var paste = Assert.IsType<Hex1bPasteEvent>(Assert.Single(events));
        var lines = new List<string>();
        await foreach (var line in paste.Paste.ReadLinesAsync())
            lines.Add(line);
        return lines;
    }

    [Fact]
    public async Task ReadLines_LfSeparator_SplitsCorrectly()
    {
        var lines = await ReadPasteLinesAsync("line1\nline2\nline3");

        Assert.Equal(3, lines.Count);
        Assert.Equal("line1", lines[0]);
        Assert.Equal("line2", lines[1]);
        Assert.Equal("line3", lines[2]);
    }

    [Fact]
    public async Task ReadLines_CrLfSeparator_SplitsCorrectly()
    {
        var lines = await ReadPasteLinesAsync("line1\r\nline2\r\nline3");

        Assert.Equal(3, lines.Count);
        Assert.Equal("line1", lines[0]);
        Assert.Equal("line2", lines[1]);
        Assert.Equal("line3", lines[2]);
    }

    [Fact]
    public async Task ReadLines_CrSeparator_SplitsCorrectly()
    {
        var lines = await ReadPasteLinesAsync("line1\rline2\rline3");

        Assert.Equal(3, lines.Count);
        Assert.Equal("line1", lines[0]);
        Assert.Equal("line2", lines[1]);
        Assert.Equal("line3", lines[2]);
    }

    [Fact]
    public async Task ReadLines_MixedLineEndings_SplitsCorrectly()
    {
        var lines = await ReadPasteLinesAsync("a\nb\r\nc\rd");

        Assert.Equal(4, lines.Count);
        Assert.Equal("a", lines[0]);
        Assert.Equal("b", lines[1]);
        Assert.Equal("c", lines[2]);
        Assert.Equal("d", lines[3]);
    }

    [Fact]
    public async Task ReadLines_NoLineEndings_ReturnsSingleLine()
    {
        var lines = await ReadPasteLinesAsync("no newlines here");

        Assert.Single(lines);
        Assert.Equal("no newlines here", lines[0]);
    }
}
