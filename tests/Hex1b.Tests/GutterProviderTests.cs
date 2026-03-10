using Hex1b.Automation;
using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the extensible gutter system: <see cref="IGutterProvider"/>,
/// <see cref="LineNumberGutterProvider"/>, and composable gutter rendering.
/// </summary>
public class GutterProviderTests
{
    private static Hex1bColor? ToCellColor(Hex1bColor color) => color.IsDefault ? null : color;
    private static bool ColorEquals(Hex1bColor? a, Hex1bColor? b) => Nullable.Equals(a, b);

    private static (EditorNode node, Hex1bAppWorkloadAdapter workload, Hex1bTerminal terminal, Hex1bRenderContext context, Hex1bTheme theme) CreateEditor(
        string text, int width, int height, bool showLineNumbers = false, IGutterProvider[]? gutterProviders = null)
    {
        var doc = new Hex1bDocument(text);
        var state = new EditorState(doc);
        var node = new EditorNode { State = state, IsFocused = false, ShowLineNumbers = showLineNumbers };
        if (gutterProviders != null)
            node.GutterProviders = [..gutterProviders];

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

    /// <summary>
    /// Simple test gutter provider that renders a fixed character for each line.
    /// </summary>
    private sealed class FixedCharGutterProvider(char ch, int width = 1) : IGutterProvider
    {
        public int ClickedLine { get; private set; } = -1;

        public int GetWidth(IHex1bDocument document) => width;

        public void RenderLine(Hex1bRenderContext context, Hex1bTheme theme, int screenX, int screenY, int docLine, int w)
        {
            var fg = Hex1bColor.White;
            var bg = theme.Get(EditorTheme.BackgroundColor);
            var text = docLine > 0 ? new string(ch, w) : new string(' ', w);
            context.WriteClipped(screenX, screenY, $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{text}");
        }

        public bool HandleClick(int docLine)
        {
            ClickedLine = docLine;
            return true;
        }
    }

    // ── LineNumberGutterProvider tests ─────────────────────────

    [Fact]
    public void LineNumberProvider_GetWidth_ReturnsDigitsPlusSeparator()
    {
        var doc = new Hex1bDocument("line1\nline2\nline3");
        var width = LineNumberGutterProvider.Instance.GetWidth(doc);

        // 3 lines → 1 digit → min 2 digits + 1 separator = 3
        Assert.Equal(3, width);
    }

    [Fact]
    public void LineNumberProvider_GetWidth_ScalesWithLineCount()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 100).Select(i => $"line{i}"));
        var doc = new Hex1bDocument(lines);
        var width = LineNumberGutterProvider.Instance.GetWidth(doc);

        // 100 lines → 3 digits + 1 separator = 4
        Assert.Equal(4, width);
    }

    [Fact]
    public void LineNumberProvider_GetWidth_Minimum2Digits()
    {
        var doc = new Hex1bDocument("single line");
        var width = LineNumberGutterProvider.Instance.GetWidth(doc);

        // 1 line → min 2 digits + 1 separator = 3
        Assert.Equal(3, width);
    }

    [Fact]
    public async Task ShowLineNumbers_RendersIdenticallyAfterRefactor()
    {
        // Original API: ShowLineNumbers = true should render line numbers via the provider
        var (node, workload, terminal, context, theme) = CreateEditor("abc\ndef\nghi", 20, 5, showLineNumbers: true);

        node.Render(context);

        // Line numbers should appear: " 1 abc", " 2 def", " 3 ghi"
        var pattern = new CellPatternSearcher().Find("1 abc");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "line numbers rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Gutter width = 3 (min 2 digits + 1 separator)
        // Line 1: " 1 abc"
        Assert.Equal(" ", snapshot.GetCell(0, 0).Character); // padding
        Assert.Equal("1", snapshot.GetCell(1, 0).Character); // digit
        Assert.Equal(" ", snapshot.GetCell(2, 0).Character); // separator
        Assert.Equal("a", snapshot.GetCell(3, 0).Character); // content starts

        // Line 2: " 2 def"
        Assert.Equal("2", snapshot.GetCell(1, 1).Character);
        Assert.Equal("d", snapshot.GetCell(3, 1).Character);

        // Line 3: " 3 ghi"
        Assert.Equal("3", snapshot.GetCell(1, 2).Character);
        Assert.Equal("g", snapshot.GetCell(3, 2).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task ShowLineNumbers_GutterUsesThemeColor()
    {
        var (node, workload, terminal, context, theme) = CreateEditor("abc", 20, 3, showLineNumbers: true);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("1 abc");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "line number rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Line number should use GutterTheme.LineNumberForegroundColor
        var expectedFg = ToCellColor(theme.Get(GutterTheme.LineNumberForegroundColor));
        var cell = snapshot.GetCell(1, 0); // the "1" digit
        Assert.True(ColorEquals(expectedFg, cell.Foreground),
            $"Expected gutter fg {expectedFg}, got {cell.Foreground}");

        workload.Dispose();
        terminal.Dispose();
    }

    // ── Custom gutter provider tests ──────────────────────────

    [Fact]
    public async Task CustomGutterProvider_RendersAtCorrectPosition()
    {
        var provider = new FixedCharGutterProvider('*');
        var (node, workload, terminal, context, theme) = CreateEditor("abc\ndef", 20, 3, gutterProviders: [provider]);

        node.Render(context);

        // Provider renders '*' at column 0, content starts at column 1
        var pattern = new CellPatternSearcher().Find("*abc");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "custom gutter rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        Assert.Equal("*", snapshot.GetCell(0, 0).Character);
        Assert.Equal("a", snapshot.GetCell(1, 0).Character);
        Assert.Equal("*", snapshot.GetCell(0, 1).Character);
        Assert.Equal("d", snapshot.GetCell(1, 1).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task MultipleProviders_RenderLeftToRight()
    {
        var provider1 = new FixedCharGutterProvider('A');
        var provider2 = new FixedCharGutterProvider('B');
        var (node, workload, terminal, context, theme) = CreateEditor("xyz", 20, 3, gutterProviders: [provider1, provider2]);

        node.Render(context);

        // Provider1 at col 0, Provider2 at col 1, content at col 2
        var pattern = new CellPatternSearcher().Find("ABxyz");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "two providers rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        Assert.Equal("A", snapshot.GetCell(0, 0).Character);
        Assert.Equal("B", snapshot.GetCell(1, 0).Character);
        Assert.Equal("x", snapshot.GetCell(2, 0).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task GutterProvider_PastEndOfDocument_RendersBlank()
    {
        var provider = new FixedCharGutterProvider('*');
        // 1 line of content, 3 lines of viewport → lines 2-3 past end
        var (node, workload, terminal, context, theme) = CreateEditor("abc", 20, 3, gutterProviders: [provider]);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("*abc");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "gutter rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Line 0: '*' (docLine > 0)
        Assert.Equal("*", snapshot.GetCell(0, 0).Character);
        // Lines 1-2: ' ' (docLine == 0, past end of document)
        Assert.Equal(" ", snapshot.GetCell(0, 1).Character);
        Assert.Equal(" ", snapshot.GetCell(0, 2).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task GutterProvider_ReducesViewportColumns()
    {
        // With a 2-wide gutter provider, content area should be reduced
        var provider = new FixedCharGutterProvider('X', width: 3);
        // 10 col viewport, 3 col gutter = 7 col content
        var (node, workload, terminal, context, theme) = CreateEditor("abcdefghij", 10, 3, gutterProviders: [provider]);

        node.Render(context);

        // Content should start at col 3 and be truncated at viewport boundary
        var pattern = new CellPatternSearcher().Find("XXX");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "gutter rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        Assert.Equal("X", snapshot.GetCell(0, 0).Character);
        Assert.Equal("X", snapshot.GetCell(1, 0).Character);
        Assert.Equal("X", snapshot.GetCell(2, 0).Character);
        Assert.Equal("a", snapshot.GetCell(3, 0).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task NoGutterProviders_NoShowLineNumbers_NoGutter()
    {
        // No providers and ShowLineNumbers=false → no gutter, content starts at col 0
        var (node, workload, terminal, context, theme) = CreateEditor("hello", 20, 3);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("hello");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "no gutter rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        Assert.Equal("h", snapshot.GetCell(0, 0).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task ExplicitGutterProvider_OverridesShowLineNumbers()
    {
        // When explicit providers are set, they take priority over ShowLineNumbers
        var provider = new FixedCharGutterProvider('#');
        var (node, workload, terminal, context, theme) = CreateEditor("abc", 20, 3,
            showLineNumbers: true, gutterProviders: [provider]);

        node.Render(context);

        // Should show '#' from provider, NOT line numbers
        var pattern = new CellPatternSearcher().Find("#abc");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "custom provider rendered instead of line numbers")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        Assert.Equal("#", snapshot.GetCell(0, 0).Character);
        Assert.Equal("a", snapshot.GetCell(1, 0).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public void WidgetGutterApi_AccumulatesProviders()
    {
        var provider1 = new FixedCharGutterProvider('A');
        var provider2 = new FixedCharGutterProvider('B');
        var doc = new Hex1bDocument("test");
        var state = new EditorState(doc);

        var widget = new EditorWidget(state).Gutter(provider1).Gutter(provider2);

        Assert.NotNull(widget.GutterProvidersValue);
        Assert.Equal(2, widget.GutterProvidersValue!.Count);
    }
}
