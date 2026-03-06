using Hex1b.Automation;
using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the text decoration rendering pipeline.
/// Verifies that <see cref="ITextDecorationProvider"/> decorations are correctly
/// applied as foreground/background colors in rendered editor cells.
/// </summary>
public class TextDecorationTests
{
    private static Hex1bColor? ToCellColor(Hex1bColor color) => color.IsDefault ? null : color;
    private static bool ColorEquals(Hex1bColor? a, Hex1bColor? b) => Nullable.Equals(a, b);

    /// <summary>
    /// Simple decoration provider that returns a fixed set of decoration spans.
    /// </summary>
    private sealed class StaticDecorationProvider(IReadOnlyList<TextDecorationSpan> spans) : ITextDecorationProvider
    {
        public IReadOnlyList<TextDecorationSpan> GetDecorations(int startLine, int endLine, IHex1bDocument document) => spans;
    }

    private static (EditorNode node, Hex1bAppWorkloadAdapter workload, Hex1bTerminal terminal, Hex1bRenderContext context, Hex1bTheme theme) CreateEditor(
        string text, int width, int height, bool focused = true, ITextDecorationProvider? decorationProvider = null)
    {
        var doc = new Hex1bDocument(text);
        var state = new EditorState(doc);
        var node = new EditorNode { State = state, IsFocused = focused };
        if (decorationProvider != null)
            node.DecorationProviders = [decorationProvider];

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

    [Fact]
    public async Task Decoration_ForegroundColor_AppliedToDecoratedCells()
    {
        var decorationColor = Hex1bColor.FromRgb(86, 156, 214); // keyword blue
        var provider = new StaticDecorationProvider([
            new TextDecorationSpan(
                new DocumentPosition(1, 1),
                new DocumentPosition(1, 4), // covers "int" (columns 1-3)
                new TextDecoration { Foreground = decorationColor })
        ]);

        // "int x" - decorate "int" with blue foreground
        var (node, workload, terminal, context, theme) = CreateEditor("int x", 20, 3, focused: false, provider);

        node.Render(context);

        // Wait for text to render
        var pattern = new CellPatternSearcher().Find("int x");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // "i", "n", "t" at columns 0-2 should have the decoration foreground
        var expectedFg = ToCellColor(decorationColor);
        for (var x = 0; x <= 2; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(ColorEquals(expectedFg, cell.Foreground),
                $"Column {x}: expected fg {expectedFg}, got {cell.Foreground}");
        }

        // " " at column 3 and "x" at column 4 should have default editor fg
        var editorFg = ToCellColor(theme.Get(EditorTheme.ForegroundColor));
        for (var x = 3; x <= 4; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(ColorEquals(editorFg, cell.Foreground),
                $"Column {x}: expected editor fg, got {cell.Foreground}");
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Decoration_BackgroundColor_AppliedToDecoratedCells()
    {
        var bgColor = Hex1bColor.FromRgb(100, 50, 50); // reddish background
        var provider = new StaticDecorationProvider([
            new TextDecorationSpan(
                new DocumentPosition(1, 1),
                new DocumentPosition(1, 4),
                new TextDecoration { Background = bgColor })
        ]);

        var (node, workload, terminal, context, theme) = CreateEditor("abc def", 20, 3, focused: false, provider);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("abc def");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        var expectedBg = ToCellColor(bgColor);
        for (var x = 0; x <= 2; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(ColorEquals(expectedBg, cell.Background),
                $"Column {x}: expected bg {expectedBg}, got {cell.Background}");
        }

        // Column 3+ should have default editor bg
        var editorBg = ToCellColor(theme.Get(EditorTheme.BackgroundColor));
        var undecorated = snapshot.GetCell(4, 0);
        Assert.True(ColorEquals(editorBg, undecorated.Background),
            $"Column 4: expected editor bg {editorBg}, got {undecorated.Background}");

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Decoration_CursorOverridesDecoration_CursorColorWins()
    {
        var decorationColor = Hex1bColor.FromRgb(86, 156, 214);
        var provider = new StaticDecorationProvider([
            new TextDecorationSpan(
                new DocumentPosition(1, 1),
                new DocumentPosition(1, 6),
                new TextDecoration { Foreground = decorationColor })
        ]);

        // Focused editor with cursor at position 0 (first char)
        var (node, workload, terminal, context, theme) = CreateEditor("Hello", 20, 3, focused: true, provider);
        var (_, _, cursorFg, cursorBg) = (
            ToCellColor(theme.Get(EditorTheme.ForegroundColor)),
            ToCellColor(theme.Get(EditorTheme.BackgroundColor)),
            ToCellColor(theme.Get(EditorTheme.CursorForegroundColor)),
            ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor))
        );

        node.Render(context);

        var cursorPattern = new CellPatternSearcher()
            .Find(ctx => ctx.Cell.Character == "H"
                      && ColorEquals(ctx.Cell.Foreground, cursorFg)
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(cursorPattern).HasMatches,
                TimeSpan.FromSeconds(2), "cursor H with cursor colors")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Cursor cell should have cursor colors, NOT decoration colors
        var cursorCell = snapshot.GetCell(0, 0);
        Assert.True(ColorEquals(cursorFg, cursorCell.Foreground),
            $"Cursor cell: expected cursor fg, got {cursorCell.Foreground}");
        Assert.True(ColorEquals(cursorBg, cursorCell.Background),
            $"Cursor cell: expected cursor bg, got {cursorCell.Background}");

        // Non-cursor decorated cells should have decoration color
        var expectedDecFg = ToCellColor(decorationColor);
        for (var x = 1; x <= 4; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(ColorEquals(expectedDecFg, cell.Foreground),
                $"Column {x}: expected decoration fg {expectedDecFg}, got {cell.Foreground}");
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Decoration_MultipleProviders_HigherPriorityWins()
    {
        var lowColor = Hex1bColor.FromRgb(100, 100, 100); // gray
        var highColor = Hex1bColor.FromRgb(255, 0, 0);    // red

        var lowProvider = new StaticDecorationProvider([
            new TextDecorationSpan(
                new DocumentPosition(1, 1),
                new DocumentPosition(1, 6),
                new TextDecoration { Foreground = lowColor },
                Priority: 0)
        ]);
        var highProvider = new StaticDecorationProvider([
            new TextDecorationSpan(
                new DocumentPosition(1, 1),
                new DocumentPosition(1, 4), // covers "Hel"
                new TextDecoration { Foreground = highColor },
                Priority: 10)
        ]);

        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        var node = new EditorNode
        {
            State = state,
            IsFocused = false,
            DecorationProviders = [lowProvider, highProvider]
        };

        var theme = Hex1bThemes.Default;
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(20, 3)
            .Build();
        var context = new Hex1bRenderContext(workload, theme);

        node.Measure(new Constraints(0, 20, 0, 3));
        node.Arrange(new Rect(0, 0, 20, 3));
        node.Render(context);

        var pattern = new CellPatternSearcher().Find("Hello");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        var expectedHigh = ToCellColor(highColor);
        var expectedLow = ToCellColor(lowColor);

        // Columns 0-2 ("Hel") should have high priority red
        for (var x = 0; x <= 2; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(ColorEquals(expectedHigh, cell.Foreground),
                $"Column {x}: expected high priority fg {expectedHigh}, got {cell.Foreground}");
        }

        // Columns 3-4 ("lo") should have low priority gray
        for (var x = 3; x <= 4; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(ColorEquals(expectedLow, cell.Foreground),
                $"Column {x}: expected low priority fg {expectedLow}, got {cell.Foreground}");
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Decoration_ThemeElement_ResolvesFromTheme()
    {
        var provider = new StaticDecorationProvider([
            new TextDecorationSpan(
                new DocumentPosition(1, 1),
                new DocumentPosition(1, 6),
                new TextDecoration { ForegroundThemeElement = SyntaxTheme.KeywordColor })
        ]);

        var (node, workload, terminal, context, theme) = CreateEditor("using", 20, 3, focused: false, provider);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("using");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        var expectedFg = ToCellColor(theme.Get(SyntaxTheme.KeywordColor));
        for (var x = 0; x <= 4; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(ColorEquals(expectedFg, cell.Foreground),
                $"Column {x}: expected keyword fg {expectedFg}, got {cell.Foreground}");
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Decoration_MultiLine_DecoratesAcrossLines()
    {
        var color = Hex1bColor.FromRgb(206, 145, 120); // string brown
        var provider = new StaticDecorationProvider([
            new TextDecorationSpan(
                new DocumentPosition(1, 2), // starts at column 2 of line 1
                new DocumentPosition(2, 4), // ends at column 4 of line 2
                new TextDecoration { Foreground = color })
        ]);

        var (node, workload, terminal, context, theme) = CreateEditor("abcde\nfghij", 20, 3, focused: false, provider);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("abcde");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var expectedFg = ToCellColor(color);
        var editorFg = ToCellColor(theme.Get(EditorTheme.ForegroundColor));

        // Line 1: column 0 ('a') = no decoration, columns 1-4 ('bcde') = decorated
        Assert.True(ColorEquals(editorFg, snapshot.GetCell(0, 0).Foreground), "Line 1 col 0 should be editor fg");
        for (var x = 1; x <= 4; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(ColorEquals(expectedFg, cell.Foreground),
                $"Line 1 col {x}: expected decoration fg, got {cell.Foreground}");
        }

        // Line 2: columns 0-2 ('fgh') = decorated, columns 3-4 ('ij') = no decoration
        for (var x = 0; x <= 2; x++)
        {
            var cell = snapshot.GetCell(x, 1);
            Assert.True(ColorEquals(expectedFg, cell.Foreground),
                $"Line 2 col {x}: expected decoration fg, got {cell.Foreground}");
        }
        Assert.True(ColorEquals(editorFg, snapshot.GetCell(3, 1).Foreground), "Line 2 col 3 should be editor fg");

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Decoration_NoDecorations_RendersSameAsWithout()
    {
        // Empty provider should render identically to no provider
        var provider = new StaticDecorationProvider([]);
        var (node, workload, terminal, context, theme) = CreateEditor("Hello World", 20, 3, focused: false, provider);
        var editorFg = ToCellColor(theme.Get(EditorTheme.ForegroundColor));
        var editorBg = ToCellColor(theme.Get(EditorTheme.BackgroundColor));

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("Hello World");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        for (var x = 0; x <= 10; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(ColorEquals(editorFg, cell.Foreground),
                $"Column {x}: expected editor fg, got {cell.Foreground}");
            Assert.True(ColorEquals(editorBg, cell.Background),
                $"Column {x}: expected editor bg, got {cell.Background}");
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Decoration_ForegroundAndBackground_BothApplied()
    {
        var fgColor = Hex1bColor.FromRgb(255, 255, 0);  // yellow
        var bgColor = Hex1bColor.FromRgb(50, 50, 100);   // dark blue

        var provider = new StaticDecorationProvider([
            new TextDecorationSpan(
                new DocumentPosition(1, 1),
                new DocumentPosition(1, 4),
                new TextDecoration { Foreground = fgColor, Background = bgColor })
        ]);

        var (node, workload, terminal, context, _) = CreateEditor("abc", 20, 3, focused: false, provider);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("abc");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        var expectedFg = ToCellColor(fgColor);
        var expectedBg = ToCellColor(bgColor);

        for (var x = 0; x <= 2; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(ColorEquals(expectedFg, cell.Foreground),
                $"Column {x}: expected fg {expectedFg}, got {cell.Foreground}");
            Assert.True(ColorEquals(expectedBg, cell.Background),
                $"Column {x}: expected bg {expectedBg}, got {cell.Background}");
        }

        workload.Dispose();
        terminal.Dispose();
    }
}
