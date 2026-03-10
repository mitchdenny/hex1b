using Hex1b.Automation;
using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for range highlights — temporary background-colored document ranges
/// used for search results, symbol occurrences, and definition flashes.
/// </summary>
public class RangeHighlightTests
{
    private static Hex1bColor? ToCellColor(Hex1bColor color) => color.IsDefault ? null : color;
    private static bool ColorEquals(Hex1bColor? a, Hex1bColor? b) => Nullable.Equals(a, b);

    private static (EditorNode node, Hex1bAppWorkloadAdapter workload, Hex1bTerminal terminal, Hex1bRenderContext context, Hex1bTheme theme) CreateEditor(
        string text, int width, int height)
    {
        var doc = new Hex1bDocument(text);
        var state = new EditorState(doc);
        var node = new EditorNode { State = state, IsFocused = false };

        var theme = Hex1bThemes.Default;
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(width, height)
            .Build();
        var context = new Hex1bRenderContext(workload, theme);

        node.Measure(new Constraints(0, width, 0, height));
        node.Arrange(new Rect(0, 0, width, height));

        return (node, workload, terminal, context, theme);
    }

    // ── State management tests ────────────────────────────────

    [Fact]
    public void PushRangeHighlights_StoresHighlightsOnSession()
    {
        var doc = new Hex1bDocument("hello world");
        var state = new EditorState(doc);
        var node = new EditorNode { State = state };
        var session = (IEditorSession)node;

        var highlights = new[]
        {
            new RangeHighlight(new DocumentPosition(1, 1), new DocumentPosition(1, 6)),
            new RangeHighlight(new DocumentPosition(1, 7), new DocumentPosition(1, 12))
        };

        session.PushRangeHighlights(highlights);
        Assert.Equal(2, session.ActiveRangeHighlights.Count);
    }

    [Fact]
    public void PushRangeHighlights_ReplacesPrevious()
    {
        var doc = new Hex1bDocument("test");
        var state = new EditorState(doc);
        var node = new EditorNode { State = state };
        var session = (IEditorSession)node;

        session.PushRangeHighlights([new RangeHighlight(new DocumentPosition(1, 1), new DocumentPosition(1, 3))]);
        session.PushRangeHighlights([new RangeHighlight(new DocumentPosition(1, 2), new DocumentPosition(1, 4))]);

        Assert.Single(session.ActiveRangeHighlights);
        Assert.Equal(2, session.ActiveRangeHighlights[0].Start.Column);
    }

    [Fact]
    public void ClearRangeHighlights_RemovesAll()
    {
        var doc = new Hex1bDocument("test");
        var state = new EditorState(doc);
        var node = new EditorNode { State = state };
        var session = (IEditorSession)node;

        session.PushRangeHighlights([new RangeHighlight(new DocumentPosition(1, 1), new DocumentPosition(1, 5))]);
        session.ClearRangeHighlights();

        Assert.Empty(session.ActiveRangeHighlights);
    }

    [Fact]
    public void ActiveRangeHighlights_InitiallyEmpty()
    {
        var doc = new Hex1bDocument("test");
        var state = new EditorState(doc);
        var node = new EditorNode { State = state };
        var session = (IEditorSession)node;

        Assert.Empty(session.ActiveRangeHighlights);
    }

    // ── Visual rendering tests ────────────────────────────────

    [Fact]
    public async Task Render_DefaultHighlight_AppliesThemeBackgroundColor()
    {
        var (node, workload, terminal, context, theme) = CreateEditor("hello world", 20, 3);
        var session = (IEditorSession)node;

        // Highlight "hello" (columns 1-5)
        session.PushRangeHighlights([
            new RangeHighlight(new DocumentPosition(1, 1), new DocumentPosition(1, 6))
        ]);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("hello world");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered with highlight")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Highlighted cells should have the default highlight background
        var expectedBg = ToCellColor(theme.Get(RangeHighlightTheme.DefaultBackground));
        for (var x = 0; x <= 4; x++) // "hello" at columns 0-4
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(ColorEquals(expectedBg, cell.Background),
                $"Column {x}: expected highlight bg {expectedBg}, got {cell.Background}");
        }

        // Non-highlighted cells should have editor background
        var editorBg = ToCellColor(theme.Get(EditorTheme.BackgroundColor));
        for (var x = 6; x <= 10; x++) // "world" at columns 6-10
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(ColorEquals(editorBg, cell.Background),
                $"Column {x}: expected editor bg, got {cell.Background}");
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_ReadAccessHighlight_UsesReadAccessColor()
    {
        var (node, workload, terminal, context, theme) = CreateEditor("abc def", 20, 3);
        var session = (IEditorSession)node;

        session.PushRangeHighlights([
            new RangeHighlight(new DocumentPosition(1, 1), new DocumentPosition(1, 4), RangeHighlightKind.ReadAccess)
        ]);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("abc def");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        var expectedBg = ToCellColor(theme.Get(RangeHighlightTheme.ReadAccessBackground));
        for (var x = 0; x <= 2; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(ColorEquals(expectedBg, cell.Background),
                $"Column {x}: expected read-access bg {expectedBg}, got {cell.Background}");
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_WriteAccessHighlight_UsesWriteAccessColor()
    {
        var (node, workload, terminal, context, theme) = CreateEditor("abc def", 20, 3);
        var session = (IEditorSession)node;

        session.PushRangeHighlights([
            new RangeHighlight(new DocumentPosition(1, 5), new DocumentPosition(1, 8), RangeHighlightKind.WriteAccess)
        ]);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("abc def");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        var expectedBg = ToCellColor(theme.Get(RangeHighlightTheme.WriteAccessBackground));
        for (var x = 4; x <= 6; x++) // "def" at columns 4-6
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(ColorEquals(expectedBg, cell.Background),
                $"Column {x}: expected write-access bg {expectedBg}, got {cell.Background}");
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_ExplicitBackgroundColor_OverridesKindDefault()
    {
        var customBg = Hex1bColor.FromRgb(200, 50, 50);
        var (node, workload, terminal, context, theme) = CreateEditor("test", 20, 3);
        var session = (IEditorSession)node;

        session.PushRangeHighlights([
            new RangeHighlight(new DocumentPosition(1, 1), new DocumentPosition(1, 5)) { Background = customBg }
        ]);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("test");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered with custom bg")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        var expectedBg = ToCellColor(customBg);
        for (var x = 0; x <= 3; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(ColorEquals(expectedBg, cell.Background),
                $"Column {x}: expected custom bg {expectedBg}, got {cell.Background}");
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_MultiLineHighlight_SpansCorrectly()
    {
        var (node, workload, terminal, context, theme) = CreateEditor("abc\ndef\nghi", 20, 5);
        var session = (IEditorSession)node;

        // Highlight from line 1 col 2 to line 2 col 3 (spans two lines)
        session.PushRangeHighlights([
            new RangeHighlight(new DocumentPosition(1, 2), new DocumentPosition(2, 3))
        ]);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("abc");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        var expectedBg = ToCellColor(theme.Get(RangeHighlightTheme.DefaultBackground));
        var editorBg = ToCellColor(theme.Get(EditorTheme.BackgroundColor));

        // Line 0: 'a' no highlight, 'b' and 'c' highlighted
        Assert.True(ColorEquals(editorBg, snapshot.GetCell(0, 0).Background),
            $"Line 0 col 0: should not be highlighted");
        Assert.True(ColorEquals(expectedBg, snapshot.GetCell(1, 0).Background),
            $"Line 0 col 1: should be highlighted");
        Assert.True(ColorEquals(expectedBg, snapshot.GetCell(2, 0).Background),
            $"Line 0 col 2: should be highlighted");

        // Line 1: 'd' and 'e' highlighted (columns 1-2), 'f' not
        Assert.True(ColorEquals(expectedBg, snapshot.GetCell(0, 1).Background),
            $"Line 1 col 0: should be highlighted");
        Assert.True(ColorEquals(expectedBg, snapshot.GetCell(1, 1).Background),
            $"Line 1 col 1: should be highlighted");

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_ClearedHighlights_BackgroundReverts()
    {
        var (node, workload, terminal, context, theme) = CreateEditor("test", 20, 3);
        var session = (IEditorSession)node;

        // Render with highlight
        session.PushRangeHighlights([
            new RangeHighlight(new DocumentPosition(1, 1), new DocumentPosition(1, 5))
        ]);
        node.Render(context);

        var highlightBg = ToCellColor(theme.Get(RangeHighlightTheme.DefaultBackground));
        var pattern = new CellPatternSearcher().Find("test");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                if (!s.SearchPattern(pattern).HasMatches) return false;
                return ColorEquals(highlightBg, s.GetCell(0, 0).Background);
            },
                TimeSpan.FromSeconds(2), "highlight applied")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Clear and re-render
        session.ClearRangeHighlights();
        node.Render(context);

        var editorBg = ToCellColor(theme.Get(EditorTheme.BackgroundColor));
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                if (!s.SearchPattern(pattern).HasMatches) return false;
                return ColorEquals(editorBg, s.GetCell(0, 0).Background);
            },
                TimeSpan.FromSeconds(2), "highlight cleared")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        for (var x = 0; x <= 3; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(ColorEquals(editorBg, cell.Background),
                $"Column {x}: expected editor bg after clear, got {cell.Background}");
        }

        workload.Dispose();
        terminal.Dispose();
    }
}
