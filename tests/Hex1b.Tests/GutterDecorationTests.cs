using Hex1b.Automation;
using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for gutter decorations — icons/markers in the editor margin
/// via <see cref="DecorationGutterProvider"/>.
/// </summary>
public class GutterDecorationTests
{
    private static Hex1bColor? ToCellColor(Hex1bColor color) => color.IsDefault ? null : color;
    private static bool ColorEquals(Hex1bColor? a, Hex1bColor? b) => Nullable.Equals(a, b);

    private static (EditorNode node, Hex1bAppWorkloadAdapter workload, Hex1bTerminal terminal, Hex1bRenderContext context, Hex1bTheme theme) CreateEditor(
        string text, int width, int height, bool showLineNumbers = false)
    {
        var doc = new Hex1bDocument(text);
        var state = new EditorState(doc);
        var node = new EditorNode { State = state, IsFocused = false, ShowLineNumbers = showLineNumbers };

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
    public void PushGutterDecorations_StoresOnSession()
    {
        var doc = new Hex1bDocument("abc\ndef");
        var state = new EditorState(doc);
        var node = new EditorNode { State = state };
        var session = (IEditorSession)node;

        session.PushGutterDecorations([
            new GutterDecoration(1, '●', GutterDecorationKind.Error),
            new GutterDecoration(2, '⚠', GutterDecorationKind.Warning)
        ]);

        Assert.Equal(2, session.ActiveGutterDecorations.Count);
    }

    [Fact]
    public void ClearGutterDecorations_RemovesAll()
    {
        var doc = new Hex1bDocument("test");
        var state = new EditorState(doc);
        var node = new EditorNode { State = state };
        var session = (IEditorSession)node;

        session.PushGutterDecorations([new GutterDecoration(1, 'X')]);
        session.ClearGutterDecorations();

        Assert.Empty(session.ActiveGutterDecorations);
    }

    // ── Visual rendering tests ────────────────────────────────

    [Fact]
    public async Task Render_GutterDecoration_ShowsIconAtCorrectLine()
    {
        var (node, workload, terminal, context, theme) = CreateEditor("abc\ndef\nghi", 20, 5);
        var session = (IEditorSession)node;

        session.PushGutterDecorations([
            new GutterDecoration(2, '*')
        ]);

        // Need to re-measure/arrange after gutter width change
        node.Measure(new Constraints(0, 20, 0, 5));
        node.Arrange(new Rect(0, 0, 20, 5));
        node.Render(context);

        // Line 1: " abc" (space for empty gutter + content)
        // Line 2: "*def" (icon + content)
        var pattern = new CellPatternSearcher().Find("def");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "gutter decoration rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Line 0 (doc line 1): no decoration → space in gutter
        Assert.Equal(" ", snapshot.GetCell(0, 0).Character);
        Assert.Equal("a", snapshot.GetCell(1, 0).Character);

        // Line 1 (doc line 2): '*' decoration
        Assert.Equal("*", snapshot.GetCell(0, 1).Character);
        Assert.Equal("d", snapshot.GetCell(1, 1).Character);

        // Line 2 (doc line 3): no decoration → space
        Assert.Equal(" ", snapshot.GetCell(0, 2).Character);
        Assert.Equal("g", snapshot.GetCell(1, 2).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_ErrorDecoration_UsesThemeErrorColor()
    {
        var (node, workload, terminal, context, theme) = CreateEditor("abc", 20, 3);
        var session = (IEditorSession)node;

        session.PushGutterDecorations([
            new GutterDecoration(1, 'E', GutterDecorationKind.Error)
        ]);

        node.Measure(new Constraints(0, 20, 0, 3));
        node.Arrange(new Rect(0, 0, 20, 3));
        node.Render(context);

        var pattern = new CellPatternSearcher().Find("Eabc");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "error icon rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        var expectedFg = ToCellColor(theme.Get(GutterDecorationTheme.ErrorIconColor));
        var cell = snapshot.GetCell(0, 0);
        Assert.Equal("E", cell.Character);
        Assert.True(ColorEquals(expectedFg, cell.Foreground),
            $"Expected error color {expectedFg}, got {cell.Foreground}");

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_CustomForeground_OverridesKindDefault()
    {
        var customColor = Hex1bColor.FromRgb(0, 255, 0);
        var (node, workload, terminal, context, theme) = CreateEditor("abc", 20, 3);
        var session = (IEditorSession)node;

        session.PushGutterDecorations([
            new GutterDecoration(1, 'X') { Foreground = customColor }
        ]);

        node.Measure(new Constraints(0, 20, 0, 3));
        node.Arrange(new Rect(0, 0, 20, 3));
        node.Render(context);

        var pattern = new CellPatternSearcher().Find("Xabc");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "custom color icon rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        var expectedFg = ToCellColor(customColor);
        var cell = snapshot.GetCell(0, 0);
        Assert.True(ColorEquals(expectedFg, cell.Foreground),
            $"Expected custom fg {expectedFg}, got {cell.Foreground}");

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_WithLineNumbers_DecorationsAppearBeforeLineNumbers()
    {
        var (node, workload, terminal, context, theme) = CreateEditor("abc\ndef", 20, 3, showLineNumbers: true);
        var session = (IEditorSession)node;

        session.PushGutterDecorations([
            new GutterDecoration(1, '*')
        ]);

        node.Measure(new Constraints(0, 20, 0, 3));
        node.Arrange(new Rect(0, 0, 20, 3));
        node.Render(context);

        // Layout: [decoration col][line number cols][content]
        // Expected: "* 1 abc" (decoration + line num 2-digit padded + sep + content)
        var pattern = new CellPatternSearcher().Find("abc");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "decoration + line numbers rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Col 0: decoration icon '*'
        Assert.Equal("*", snapshot.GetCell(0, 0).Character);
        // Col 1: line number padding ' '
        Assert.Equal(" ", snapshot.GetCell(1, 0).Character);
        // Col 2: line number '1'
        Assert.Equal("1", snapshot.GetCell(2, 0).Character);
        // Col 3: separator ' '
        Assert.Equal(" ", snapshot.GetCell(3, 0).Character);
        // Col 4: content 'a'
        Assert.Equal("a", snapshot.GetCell(4, 0).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_NoDecorations_NoGutterColumn()
    {
        // Without decorations, the DecorationGutterProvider should have width 0
        var (node, workload, terminal, context, theme) = CreateEditor("abc", 20, 3);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("abc");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "no gutter rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Content starts at column 0 (no gutter)
        Assert.Equal("a", snapshot.GetCell(0, 0).Character);

        workload.Dispose();
        terminal.Dispose();
    }
}
