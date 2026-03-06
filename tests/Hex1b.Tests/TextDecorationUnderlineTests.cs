using Hex1b.Automation;
using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for underline decoration rendering and terminal capability fallback.
/// </summary>
public class TextDecorationUnderlineTests
{
    private static Hex1bColor? ToCellColor(Hex1bColor color) => color.IsDefault ? null : color;
    private static bool ColorEquals(Hex1bColor? a, Hex1bColor? b) => Nullable.Equals(a, b);

    private sealed class StaticDecorationProvider(IReadOnlyList<TextDecorationSpan> spans) : ITextDecorationProvider
    {
        public IReadOnlyList<TextDecorationSpan> GetDecorations(int startLine, int endLine, IHex1bDocument document) => spans;
    }

    private static (EditorNode node, Hex1bAppWorkloadAdapter workload, Hex1bTerminal terminal, Hex1bRenderContext context, Hex1bTheme theme) CreateEditor(
        string text, int width, int height, bool focused = true, ITextDecorationProvider? decorationProvider = null,
        TerminalCapabilities? capabilities = null)
    {
        var doc = new Hex1bDocument(text);
        var state = new EditorState(doc);
        var node = new EditorNode { State = state, IsFocused = focused };
        if (decorationProvider != null)
            node.DecorationProviders = [decorationProvider];

        var theme = Hex1bThemes.Default;
        var workload = new Hex1bAppWorkloadAdapter(capabilities);
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
    public async Task Decoration_CurlyUnderline_AppliedToDecoratedCells()
    {
        var ulColor = Hex1bColor.FromRgb(255, 0, 0); // red error underline
        var provider = new StaticDecorationProvider([
            new TextDecorationSpan(
                new DocumentPosition(1, 1),
                new DocumentPosition(1, 4), // covers "err" (columns 1-3)
                new TextDecoration
                {
                    UnderlineStyle = UnderlineStyle.Curly,
                    UnderlineColor = ulColor,
                })
        ]);

        var (node, workload, terminal, context, _) = CreateEditor("err ok", 20, 3, focused: false, provider);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("err ok");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Columns 0-2 ("err") should have curly underline with red color
        for (var x = 0; x <= 2; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.Equal(UnderlineStyle.Curly, cell.UnderlineStyle);
            Assert.True(ColorEquals(ToCellColor(ulColor), cell.UnderlineColor),
                $"Column {x}: expected underline color {ulColor}, got {cell.UnderlineColor}");
        }

        // Column 4 ("o") should not have underline
        var normalCell = snapshot.GetCell(4, 0);
        Assert.Equal(UnderlineStyle.None, normalCell.UnderlineStyle);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Decoration_SingleUnderline_AppliedToDecoratedCells()
    {
        var provider = new StaticDecorationProvider([
            new TextDecorationSpan(
                new DocumentPosition(1, 1),
                new DocumentPosition(1, 4),
                new TextDecoration { UnderlineStyle = UnderlineStyle.Single })
        ]);

        var (node, workload, terminal, context, _) = CreateEditor("abc def", 20, 3, focused: false, provider);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("abc def");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // "abc" should be underlined (single)
        for (var x = 0; x <= 2; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(cell.IsUnderline, $"Column {x}: expected underline");
        }

        // " " at column 3 should not be underlined
        Assert.False(snapshot.GetCell(3, 0).IsUnderline);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Decoration_UnderlineWithForeground_BothApplied()
    {
        var fgColor = Hex1bColor.FromRgb(86, 156, 214);
        var ulColor = Hex1bColor.FromRgb(255, 200, 0);
        var provider = new StaticDecorationProvider([
            new TextDecorationSpan(
                new DocumentPosition(1, 1),
                new DocumentPosition(1, 4),
                new TextDecoration
                {
                    Foreground = fgColor,
                    UnderlineStyle = UnderlineStyle.Curly,
                    UnderlineColor = ulColor,
                })
        ]);

        var (node, workload, terminal, context, _) = CreateEditor("var x", 20, 3, focused: false, provider);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("var x");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        for (var x = 0; x <= 2; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(ColorEquals(ToCellColor(fgColor), cell.Foreground),
                $"Column {x}: expected fg {fgColor}, got {cell.Foreground}");
            Assert.Equal(UnderlineStyle.Curly, cell.UnderlineStyle);
            Assert.True(ColorEquals(ToCellColor(ulColor), cell.UnderlineColor),
                $"Column {x}: expected underline color {ulColor}, got {cell.UnderlineColor}");
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Decoration_UnderlineColorThemeElement_ResolvesFromTheme()
    {
        var provider = new StaticDecorationProvider([
            new TextDecorationSpan(
                new DocumentPosition(1, 1),
                new DocumentPosition(1, 6),
                new TextDecoration
                {
                    UnderlineStyle = UnderlineStyle.Curly,
                    UnderlineColorThemeElement = DiagnosticTheme.ErrorUnderlineColor,
                })
        ]);

        var (node, workload, terminal, context, theme) = CreateEditor("error", 20, 3, focused: false, provider);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("error");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        var expectedUlColor = ToCellColor(theme.Get(DiagnosticTheme.ErrorUnderlineColor));
        for (var x = 0; x <= 4; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.Equal(UnderlineStyle.Curly, cell.UnderlineStyle);
            Assert.True(ColorEquals(expectedUlColor, cell.UnderlineColor),
                $"Column {x}: expected underline color {expectedUlColor}, got {cell.UnderlineColor}");
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Decoration_DottedUnderline_AppliedCorrectly()
    {
        var provider = new StaticDecorationProvider([
            new TextDecorationSpan(
                new DocumentPosition(1, 1),
                new DocumentPosition(1, 5),
                new TextDecoration { UnderlineStyle = UnderlineStyle.Dotted })
        ]);

        var (node, workload, terminal, context, _) = CreateEditor("info", 20, 3, focused: false, provider);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("info");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        for (var x = 0; x <= 3; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.Equal(UnderlineStyle.Dotted, cell.UnderlineStyle);
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Decoration_DashedUnderline_AppliedCorrectly()
    {
        var provider = new StaticDecorationProvider([
            new TextDecorationSpan(
                new DocumentPosition(1, 1),
                new DocumentPosition(1, 5),
                new TextDecoration { UnderlineStyle = UnderlineStyle.Dashed })
        ]);

        var (node, workload, terminal, context, _) = CreateEditor("hint", 20, 3, focused: false, provider);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("hint");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        for (var x = 0; x <= 3; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.Equal(UnderlineStyle.Dashed, cell.UnderlineStyle);
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Decoration_FallbackToSingleUnderline_WhenStyledUnsupported()
    {
        var provider = new StaticDecorationProvider([
            new TextDecorationSpan(
                new DocumentPosition(1, 1),
                new DocumentPosition(1, 4),
                new TextDecoration { UnderlineStyle = UnderlineStyle.Curly })
        ]);

        // Use capabilities that don't support styled underlines
        var caps = TerminalCapabilities.Modern with { SupportsStyledUnderlines = false };
        var (node, workload, terminal, context, _) = CreateEditor("abc", 20, 3, focused: false, provider, caps);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("abc");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Should fall back to single underline (the terminal parses SGR 4 as underline)
        for (var x = 0; x <= 2; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(cell.IsUnderline, $"Column {x}: expected underline (fallback from curly to single)");
        }

        workload.Dispose();
        terminal.Dispose();
    }
}
