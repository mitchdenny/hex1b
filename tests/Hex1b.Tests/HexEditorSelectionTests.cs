// Tests for hex editor cursor positioning and selection via mouse click,
// keyboard navigation, and drag selection. Uses the input sequencer and
// cell pattern searcher to verify rendered output across a matrix of scenarios.

using Hex1b.Automation;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class HexEditorSelectionTests
{
    private static Hex1bColor? ToCellColor(Hex1bColor color) => color.IsDefault ? null : color;
    private static bool ColorEquals(Hex1bColor? a, Hex1bColor? b) => Nullable.Equals(a, b);

    // Hex layout constants (from HexEditorViewRenderer)
    private const int AddressWidth = 8;
    private const int SeparatorWidth = 2;
    private const int HexStart = AddressWidth + SeparatorWidth; // 10

    private static int HexColForByte(int byteInRow) => HexStart + byteInRow * 3;

    private static int AsciiColForByte(int byteInRow, int bytesPerRow)
    {
        var hexWidth = bytesPerRow * 3 - 1;
        return HexStart + hexWidth + SeparatorWidth + byteInRow;
    }

    private static (Hex1bAppWorkloadAdapter workload, Hex1bTerminal terminal, Hex1bApp app,
        EditorState state, Hex1bTheme theme, Task runTask) SetupHexEditor(
        string text, int width = 75, int height = 10)
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(width, height).Build();

        var theme = Hex1bThemes.Default;
        var doc = new Hex1bDocument(text);
        var state = new EditorState(doc);
        var renderer = new HexEditorViewRenderer { HighlightMultiByteChars = true };

        var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state).WithViewRenderer(renderer)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        return (workload, terminal, app, state, theme, runTask);
    }

    private static (Hex1bAppWorkloadAdapter workload, Hex1bTerminal terminal, Hex1bApp app,
        EditorState state, Hex1bTheme theme, Task runTask) SetupHexEditorBytes(
        byte[] bytes, int width = 75, int height = 10)
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(width, height).Build();

        var theme = Hex1bThemes.Default;
        var doc = new Hex1bDocument(bytes);
        var state = new EditorState(doc);
        var renderer = new HexEditorViewRenderer { HighlightMultiByteChars = true };

        var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state).WithViewRenderer(renderer)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        return (workload, terminal, app, state, theme, runTask);
    }

    private static async Task WaitForHexEditor(Hex1bTerminal terminal)
    {
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen,
                TimeSpan.FromSeconds(2), "hex editor visible in alt screen")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 1: Click cursor positioning — single byte selection
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Click_OnHexByte_PositionsCursorOnThatByte(int byteIndex)
    {
        // "ABCD" = 41 42 43 44 (all single-byte ASCII)
        var (workload, terminal, app, state, theme, runTask) = SetupHexEditor("ABCD");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));
        await WaitForHexEditor(terminal);

        var hexCol = HexColForByte(byteIndex);

        // Click on the hex column for the target byte
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(hexCol, 0)
            .WaitUntil(_ => state.ByteCursorOffset == byteIndex,
                TimeSpan.FromSeconds(2), $"cursor at hex byte {byteIndex}")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(byteIndex, state.ByteCursorOffset);
        Assert.Equal(byteIndex, state.Cursor.Position.Value); // ASCII = 1 byte per char
    }

    [Fact]
    public async Task Click_OnAsciiColumn_PositionsCursorOnCorrectByte()
    {
        // "ABCD" = 41 42 43 44, click on ASCII column for byte 2
        var (workload, terminal, app, state, theme, runTask) = SetupHexEditor("ABCD");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));
        await WaitForHexEditor(terminal);

        // At width=75, bytesPerRow=16
        var asciiCol = AsciiColForByte(2, 16);

        var cursorAtAscii = new CellPatternSearcher()
            .Find(ctx => ctx.X == asciiCol && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(asciiCol, 0)
            .WaitUntil(s => s.SearchPattern(cursorAtAscii).HasMatches,
                TimeSpan.FromSeconds(2), "cursor at ASCII byte 2")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(2, state.ByteCursorOffset);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 2: Click on multi-byte characters — byte-level precision
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0, 0)] // First byte of "é" (C3) → byte 0
    [InlineData(1, 1)] // Second byte of "é" (A9) → byte 1
    [InlineData(2, 2)] // "B" (42) → byte 2
    public async Task Click_OnMultiByteContent_SelectsCorrectByte(int byteIndex, int expectedByteOffset)
    {
        // "éB" = C3 A9 42 (3 bytes, 2 chars)
        var (workload, terminal, app, state, theme, runTask) = SetupHexEditor("éB");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));
        await WaitForHexEditor(terminal);

        var hexCol = HexColForByte(byteIndex);

        var cursorAtByte = new CellPatternSearcher()
            .Find(ctx => ctx.X == hexCol && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(hexCol, 0)
            .WaitUntil(s => s.SearchPattern(cursorAtByte).HasMatches,
                TimeSpan.FromSeconds(2), $"cursor at byte {byteIndex}")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(expectedByteOffset, state.ByteCursorOffset);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 3: Arrow key navigation — byte-level movement
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ArrowRight_MovesOneByteAtATime_ThroughMultiByte()
    {
        // "é" = C3 A9 (2 bytes)
        var (workload, terminal, app, state, theme, runTask) = SetupHexEditor("é");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));
        await WaitForHexEditor(terminal);

        // Click on byte 0 (C3) to start
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(HexColForByte(0), 0)
            .WaitUntil(_ => state.ByteCursorOffset == 0,
                TimeSpan.FromSeconds(2), "cursor at byte 0")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Right arrow should move to byte 1 (A9), not jump past the character
        var cursorAtByte1 = new CellPatternSearcher()
            .Find(ctx => ctx.X == HexColForByte(1) && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .Right()
            .WaitUntil(s => s.SearchPattern(cursorAtByte1).HasMatches,
                TimeSpan.FromSeconds(2), "cursor moved to byte 1")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(1, state.ByteCursorOffset);
    }

    [Fact]
    public async Task ArrowLeft_MovesOneByteAtATime_ThroughMultiByte()
    {
        // "éB" = C3 A9 42 (3 bytes)
        var (workload, terminal, app, state, theme, runTask) = SetupHexEditor("éB");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));
        await WaitForHexEditor(terminal);

        // Click on byte 2 (42 = 'B')
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(HexColForByte(2), 0)
            .WaitUntil(_ => state.ByteCursorOffset == 2,
                TimeSpan.FromSeconds(2), "cursor at byte 2")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Left arrow to byte 1 (A9)
        var cursorAtByte1 = new CellPatternSearcher()
            .Find(ctx => ctx.X == HexColForByte(1) && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .Left()
            .WaitUntil(s => s.SearchPattern(cursorAtByte1).HasMatches,
                TimeSpan.FromSeconds(2), "cursor at byte 1")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(1, state.ByteCursorOffset);

        // Left arrow to byte 0 (C3)
        var cursorAtByte0 = new CellPatternSearcher()
            .Find(ctx => ctx.X == HexColForByte(0) && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .Left()
            .WaitUntil(s => s.SearchPattern(cursorAtByte0).HasMatches,
                TimeSpan.FromSeconds(2), "cursor at byte 0")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(0, state.ByteCursorOffset);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 4: Shift+Arrow selection — byte-level selection
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ShiftRight_SelectsOneByteAtATime()
    {
        // "ABCD" = 41 42 43 44
        var (workload, terminal, app, state, theme, runTask) = SetupHexEditor("ABCD");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var selBg = ToCellColor(theme.Get(EditorTheme.SelectionBackgroundColor));
        await WaitForHexEditor(terminal);

        // Click byte 0
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(HexColForByte(0), 0)
            .WaitUntil(_ => state.ByteCursorOffset == 0,
                TimeSpan.FromSeconds(2), "cursor at byte 0")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Shift+Right to select byte 0
        var selAtByte0 = new CellPatternSearcher()
            .Find(ctx => ctx.X == HexColForByte(0) && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, selBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow, Hex1bModifiers.Shift)
            .WaitUntil(s => s.SearchPattern(selAtByte0).HasMatches,
                TimeSpan.FromSeconds(2), "byte 0 selected")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(state.Cursor.HasSelection);
        Assert.Equal(1, state.ByteCursorOffset);
    }

    [Fact]
    public async Task ShiftRight_MultiByte_MovesByteAndTracksOffset()
    {
        // "éB" = C3 A9 42 (3 bytes, 2 chars)
        // Shift+Right from byte 0 (char 0) to byte 1 (still char 0) won't produce
        // char-level selection since both bytes map to the same char, but ByteCursorOffset
        // should still advance. A second Shift+Right to byte 2 (char 1) creates selection.
        var (workload, terminal, app, state, theme, runTask) = SetupHexEditor("éB");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        await WaitForHexEditor(terminal);

        // Click byte 0
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(HexColForByte(0), 0)
            .WaitUntil(_ => state.ByteCursorOffset == 0,
                TimeSpan.FromSeconds(2), "cursor at byte 0")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Shift+Right — byte moves to 1, char stays at 0 (within same char)
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow, Hex1bModifiers.Shift)
            .WaitUntil(_ => state.ByteCursorOffset == 1,
                TimeSpan.FromSeconds(2), "byte cursor at 1 after Shift+Right")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(1, state.ByteCursorOffset);

        // Shift+Right again — byte 2 = char 1 ('B'), now crosses char boundary = char-level selection
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow, Hex1bModifiers.Shift)
            .WaitUntil(_ => state.ByteCursorOffset == 2,
                TimeSpan.FromSeconds(2), "byte cursor at 2")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(2, state.ByteCursorOffset);
        Assert.True(state.Cursor.HasSelection, "selection should exist after crossing char boundary");
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 5: Click after navigation — ByteCursorOffset updates
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Click_AfterArrowNavigation_UpdatesByteCursorOffset()
    {
        // "ABCD" = 41 42 43 44
        var (workload, terminal, app, state, theme, runTask) = SetupHexEditor("ABCD");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));
        await WaitForHexEditor(terminal);

        // Navigate with arrows first
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(HexColForByte(0), 0)
            .WaitUntil(_ => state.ByteCursorOffset == 0,
                TimeSpan.FromSeconds(2), "start at byte 0")
            .Right().Right()
            .WaitUntil(_ => state.ByteCursorOffset == 2,
                TimeSpan.FromSeconds(2), "at byte 2 after arrows")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Now click on byte 1 — should update ByteCursorOffset
        var cursorAtByte1 = new CellPatternSearcher()
            .Find(ctx => ctx.X == HexColForByte(1) && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(HexColForByte(1), 0)
            .WaitUntil(s => s.SearchPattern(cursorAtByte1).HasMatches,
                TimeSpan.FromSeconds(2), "click moves cursor to byte 1")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(1, state.ByteCursorOffset);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 6: Invalid byte content — click and navigate
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Click_OnInvalidBytes_SelectsCorrectByte(int byteIndex)
    {
        // Raw invalid bytes: FE FF 80 (each 1 byte = 1 char)
        var (workload, terminal, app, state, theme, runTask) =
            SetupHexEditorBytes([0xFE, 0xFF, 0x80]);
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));
        await WaitForHexEditor(terminal);

        var hexCol = HexColForByte(byteIndex);
        var cursorAtByte = new CellPatternSearcher()
            .Find(ctx => ctx.X == hexCol && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(hexCol, 0)
            .WaitUntil(s => s.SearchPattern(cursorAtByte).HasMatches,
                TimeSpan.FromSeconds(2), $"cursor at invalid byte {byteIndex}")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(byteIndex, state.ByteCursorOffset);
    }

    [Fact]
    public async Task Navigate_ThroughInvalidBytes_VisitsEveryByte()
    {
        // FE FF 80 BF — 4 invalid bytes
        var (workload, terminal, app, state, theme, runTask) =
            SetupHexEditorBytes([0xFE, 0xFF, 0x80, 0xBF]);
        using var _ = workload; using var __ = terminal; using var ___ = app;

        await WaitForHexEditor(terminal);

        // Click byte 0
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(HexColForByte(0), 0)
            .WaitUntil(_ => state.ByteCursorOffset == 0,
                TimeSpan.FromSeconds(2), "start at byte 0")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Walk right through all bytes
        for (int expected = 1; expected <= 4; expected++)
        {
            var target = Math.Min(expected, 4);
            await new Hex1bTerminalInputSequenceBuilder()
                .Right()
                .WaitUntil(_ => state.ByteCursorOffset == target,
                    TimeSpan.FromSeconds(2), $"at byte {target}")
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        }

        Assert.Equal(4, state.ByteCursorOffset);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 7: Hex input + cursor advance
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task TypeHex_CommitsAndAdvancesToNextByte()
    {
        // "AB" = 41 42
        var (workload, terminal, app, state, theme, runTask) = SetupHexEditor("AB");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));
        await WaitForHexEditor(terminal);

        // Click byte 0
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(HexColForByte(0), 0)
            .WaitUntil(_ => state.ByteCursorOffset == 0,
                TimeSpan.FromSeconds(2), "at byte 0")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Type "FF" — should replace byte 0 and advance to byte 1
        var cursorAtByte1 = new CellPatternSearcher()
            .Find(ctx => ctx.X == HexColForByte(1) && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .Type("FF")
            .WaitUntil(s => s.SearchPattern(cursorAtByte1).HasMatches,
                TimeSpan.FromSeconds(2), "cursor advanced to byte 1 after typing FF")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(1, state.ByteCursorOffset);
        Assert.Equal(0xFF, state.Document.GetBytes().Span[0]);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 8: Cursor highlight rendering — verify visual
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task CursorHighlight_AppearOnBothHexAndAsciiColumns()
    {
        // "A" = 41 (1 byte)
        var (workload, terminal, app, state, theme, runTask) = SetupHexEditor("A");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));
        await WaitForHexEditor(terminal);

        var hexCol = HexColForByte(0);
        var asciiCol = AsciiColForByte(0, 16);

        // Cursor should appear in both hex and ASCII columns
        var cursorInHex = new CellPatternSearcher()
            .Find(ctx => ctx.X == hexCol && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        var cursorInAscii = new CellPatternSearcher()
            .Find(ctx => ctx.X == asciiCol && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(hexCol, 0)
            .WaitUntil(s => s.SearchPattern(cursorInHex).HasMatches
                         && s.SearchPattern(cursorInAscii).HasMatches,
                TimeSpan.FromSeconds(2), "cursor in both hex and ASCII columns")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
    }
}
