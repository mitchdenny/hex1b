using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for ED mode 3 (CSI 3J) — erase scrollback buffer.
/// Verifies that CSI 3J clears the scrollback without affecting the visible screen,
/// and that it differs from CSI 2J which only clears the visible screen.
/// Inspired by psmux's squelch signal tests for CSI 2J/3J discrimination.
/// </summary>
public class EraseScrollbackTests
{
    private sealed class TestTerminal : IDisposable
    {
        private readonly StreamWorkloadAdapter _workload;
        public Hex1bTerminal Terminal { get; }

        public TestTerminal(int width = 40, int height = 5)
        {
            _workload = StreamWorkloadAdapter.CreateHeadless(width, height);
            Terminal = Hex1bTerminal.CreateBuilder()
                .WithWorkload(_workload)
                .WithHeadless()
                .WithDimensions(width, height)
                .WithScrollback(100)
                .Build();
        }

        public void Write(string text)
        {
            // Simulate PTY ONLCR translation
            var translated = text.Replace("\n", "\r\n");
            Terminal.ApplyTokens(AnsiTokenizer.Tokenize(translated));
        }

        public void Dispose()
        {
            Terminal.Dispose();
            _workload.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    [Fact]
    public void Ed3_ClearsScrollback()
    {
        using var t = new TestTerminal(width: 40, height: 5);

        // Write enough lines to fill screen and push some into scrollback
        for (int i = 0; i < 10; i++)
            t.Write($"Line {i}\n");

        // Verify scrollback has content
        var snapBefore = t.Terminal.CreateSnapshot(scrollbackLines: 100);
        Assert.True(snapBefore.ScrollbackLineCount > 0,
            "Should have scrollback lines before CSI 3J");

        // CSI 3J — erase scrollback
        t.Write("\x1b[3J");

        // Scrollback should be cleared
        var snapAfter = t.Terminal.CreateSnapshot(scrollbackLines: 100);
        Assert.Equal(0, snapAfter.ScrollbackLineCount);
    }

    [Fact]
    public void Ed2_DoesNotClearScrollback()
    {
        using var t = new TestTerminal(width: 40, height: 5);

        // Push content into scrollback
        for (int i = 0; i < 10; i++)
            t.Write($"Line {i}\n");

        var snapBefore = t.Terminal.CreateSnapshot(scrollbackLines: 100);
        var scrollbackBefore = snapBefore.ScrollbackLineCount;
        Assert.True(scrollbackBefore > 0);

        // CSI 2J — erase visible screen only
        t.Write("\x1b[2J");

        // Scrollback should NOT be cleared
        var snapAfter = t.Terminal.CreateSnapshot(scrollbackLines: 100);
        Assert.True(snapAfter.ScrollbackLineCount > 0,
            "CSI 2J should not clear scrollback");
    }

    [Fact]
    public void Ed3_PreservesCursorPosition()
    {
        using var t = new TestTerminal(width: 40, height: 5);

        // Push content into scrollback
        for (int i = 0; i < 10; i++)
            t.Write($"Line {i}\n");

        // Position cursor
        t.Write("\x1b[2;5H");

        // Capture cursor position before
        var snapBefore = t.Terminal.CreateSnapshot();
        var cursorXBefore = snapBefore.CursorX;
        var cursorYBefore = snapBefore.CursorY;

        // CSI 3J
        t.Write("\x1b[3J");

        // Cursor should be preserved
        var snapAfter = t.Terminal.CreateSnapshot();
        Assert.Equal(cursorXBefore, snapAfter.CursorX);
        Assert.Equal(cursorYBefore, snapAfter.CursorY);
    }

    [Fact]
    public void Ed3_CombinedWithEd2_ClearsBothScreenAndScrollback()
    {
        using var t = new TestTerminal(width: 40, height: 5);

        // Push content into scrollback
        for (int i = 0; i < 10; i++)
            t.Write($"Line {i}\n");

        Assert.True(t.Terminal.CreateSnapshot(scrollbackLines: 100).ScrollbackLineCount > 0);

        // CSI 2J + CSI 3J — clear both visible screen and scrollback
        t.Write("\x1b[2J\x1b[3J");

        var snap = t.Terminal.CreateSnapshot(scrollbackLines: 100);
        Assert.Equal(0, snap.ScrollbackLineCount);
    }
}
