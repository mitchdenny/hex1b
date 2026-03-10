using Hex1b.Automation;
using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for inline hints — virtual text rendered inline at document positions
/// without modifying the document model.
/// </summary>
public class InlineHintTests
{
    private static Hex1bColor? ToCellColor(Hex1bColor color) => color.IsDefault ? null : color;
    private static bool ColorEquals(Hex1bColor? a, Hex1bColor? b) => Nullable.Equals(a, b);

    private static (EditorNode node, Hex1bAppWorkloadAdapter workload, Hex1bTerminal terminal, Hex1bRenderContext context, Hex1bTheme theme) CreateEditor(
        string text, int width, int height, bool focused = true)
    {
        var doc = new Hex1bDocument(text);
        var state = new EditorState(doc);
        var node = new EditorNode { State = state, IsFocused = focused };

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

    // ── IEditorSession state management tests ─────────────────────

    [Fact]
    public void PushInlineHints_StoresHintsOnSession()
    {
        var doc = new Hex1bDocument("hello world");
        var state = new EditorState(doc);
        var node = new EditorNode { State = state };
        var session = (IEditorSession)node;

        var hints = new[]
        {
            new InlineHint(new DocumentPosition(1, 6), ": string"),
            new InlineHint(new DocumentPosition(1, 12), " -> int")
        };

        session.PushInlineHints(hints);

        Assert.Equal(2, session.ActiveInlineHints.Count);
        Assert.Equal(": string", session.ActiveInlineHints[0].Text);
        Assert.Equal(" -> int", session.ActiveInlineHints[1].Text);
    }

    [Fact]
    public void PushInlineHints_ReplacesPreviousHints()
    {
        var doc = new Hex1bDocument("x = 5");
        var state = new EditorState(doc);
        var node = new EditorNode { State = state };
        var session = (IEditorSession)node;

        session.PushInlineHints([new InlineHint(new DocumentPosition(1, 1), " (any)")]);
        session.PushInlineHints([new InlineHint(new DocumentPosition(1, 1), " (number)")]);

        Assert.Single(session.ActiveInlineHints);
        Assert.Equal(" (number)", session.ActiveInlineHints[0].Text);
    }

    [Fact]
    public void ClearInlineHints_RemovesAllHints()
    {
        var doc = new Hex1bDocument("x = 5");
        var state = new EditorState(doc);
        var node = new EditorNode { State = state };
        var session = (IEditorSession)node;

        session.PushInlineHints([new InlineHint(new DocumentPosition(1, 1), " (int)")]);
        Assert.Single(session.ActiveInlineHints);

        session.ClearInlineHints();

        Assert.Empty(session.ActiveInlineHints);
    }

    [Fact]
    public void ActiveInlineHints_InitiallyEmpty()
    {
        var doc = new Hex1bDocument("test");
        var state = new EditorState(doc);
        var node = new EditorNode { State = state };
        var session = (IEditorSession)node;

        Assert.Empty(session.ActiveInlineHints);
    }

    // ── Visual rendering tests ────────────────────────────────────

    [Fact]
    public async Task Render_InlineHint_AppearsAtCorrectPosition()
    {
        // "hello" with hint ": str" at column 6 (after 'o') → "hello: str"
        var (node, workload, terminal, context, theme) = CreateEditor("hello", 30, 3, focused: false);
        var session = (IEditorSession)node;

        session.PushInlineHints([
            new InlineHint(new DocumentPosition(1, 6), ": str")
        ]);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("hello: str");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "hint text rendered after 'hello'")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Verify original text is at expected positions
        Assert.Equal("h", snapshot.GetCell(0, 0).Character);
        Assert.Equal("e", snapshot.GetCell(1, 0).Character);
        Assert.Equal("l", snapshot.GetCell(2, 0).Character);
        Assert.Equal("l", snapshot.GetCell(3, 0).Character);
        Assert.Equal("o", snapshot.GetCell(4, 0).Character);

        // Verify hint text follows immediately
        Assert.Equal(":", snapshot.GetCell(5, 0).Character);
        Assert.Equal(" ", snapshot.GetCell(6, 0).Character);
        Assert.Equal("s", snapshot.GetCell(7, 0).Character);
        Assert.Equal("t", snapshot.GetCell(8, 0).Character);
        Assert.Equal("r", snapshot.GetCell(9, 0).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_InlineHint_ShiftsSubsequentTextRight()
    {
        // "ab" with hint "XX" at column 2 (before 'b') → "aXXb"
        var (node, workload, terminal, context, theme) = CreateEditor("ab", 30, 3, focused: false);
        var session = (IEditorSession)node;

        session.PushInlineHints([
            new InlineHint(new DocumentPosition(1, 2), "XX")
        ]);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("aXXb");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "hint inserted before 'b'")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        Assert.Equal("a", snapshot.GetCell(0, 0).Character);
        Assert.Equal("X", snapshot.GetCell(1, 0).Character);
        Assert.Equal("X", snapshot.GetCell(2, 0).Character);
        Assert.Equal("b", snapshot.GetCell(3, 0).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_InlineHint_UsesThemeForegroundColor()
    {
        var (node, workload, terminal, context, theme) = CreateEditor("hello", 30, 3, focused: false);
        var session = (IEditorSession)node;

        session.PushInlineHints([
            new InlineHint(new DocumentPosition(1, 6), ": int")
        ]);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("hello: int");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "hint text rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Hint characters should use InlineHintTheme foreground color
        var expectedHintFg = ToCellColor(theme.Get(InlineHintTheme.ForegroundColor));
        for (var x = 5; x <= 9; x++) // ": int" at columns 5-9
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(ColorEquals(expectedHintFg, cell.Foreground),
                $"Hint column {x}: expected hint fg {expectedHintFg}, got {cell.Foreground}");
        }

        // Original text should use editor foreground, not hint foreground
        var editorFg = ToCellColor(theme.Get(EditorTheme.ForegroundColor));
        for (var x = 0; x <= 4; x++) // "hello" at columns 0-4
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(ColorEquals(editorFg, cell.Foreground),
                $"Text column {x}: expected editor fg {editorFg}, got {cell.Foreground}");
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_InlineHint_CustomDecorationOverridesTheme()
    {
        var customColor = Hex1bColor.FromRgb(255, 100, 0); // orange
        var (node, workload, terminal, context, theme) = CreateEditor("test", 30, 3, focused: false);
        var session = (IEditorSession)node;

        session.PushInlineHints([
            new InlineHint(
                new DocumentPosition(1, 5), " OK",
                new TextDecoration { Foreground = customColor })
        ]);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("test OK");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "hint with custom color rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Hint characters should use the custom decoration color, not theme default
        var expectedFg = ToCellColor(customColor);
        for (var x = 4; x <= 6; x++) // " OK" at columns 4-6
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(ColorEquals(expectedFg, cell.Foreground),
                $"Hint column {x}: expected custom fg {expectedFg}, got {cell.Foreground}");
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_MultipleHintsOnSameLine_AllRendered()
    {
        // "a b c" with hints after each: "a[X] b[Y] c"
        var (node, workload, terminal, context, theme) = CreateEditor("a b c", 30, 3, focused: false);
        var session = (IEditorSession)node;

        session.PushInlineHints([
            new InlineHint(new DocumentPosition(1, 2), "X"),
            new InlineHint(new DocumentPosition(1, 4), "Y")
        ]);

        node.Render(context);

        // After hints: "aX bY c"
        var pattern = new CellPatternSearcher().Find("aX bY c");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "multiple hints rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        Assert.Equal("a", snapshot.GetCell(0, 0).Character);
        Assert.Equal("X", snapshot.GetCell(1, 0).Character);
        Assert.Equal(" ", snapshot.GetCell(2, 0).Character);
        Assert.Equal("b", snapshot.GetCell(3, 0).Character);
        Assert.Equal("Y", snapshot.GetCell(4, 0).Character);
        Assert.Equal(" ", snapshot.GetCell(5, 0).Character);
        Assert.Equal("c", snapshot.GetCell(6, 0).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_HintOnDifferentLines_RenderedOnCorrectLines()
    {
        var (node, workload, terminal, context, theme) = CreateEditor("abc\ndef", 30, 5, focused: false);
        var session = (IEditorSession)node;

        session.PushInlineHints([
            new InlineHint(new DocumentPosition(1, 4), "X"),
            new InlineHint(new DocumentPosition(2, 4), "Y")
        ]);

        node.Render(context);

        // Line 1: "abcX", Line 2: "defY"
        var pattern1 = new CellPatternSearcher().Find("abcX");
        var pattern2 = new CellPatternSearcher().Find("defY");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern1).HasMatches && s.SearchPattern(pattern2).HasMatches,
                TimeSpan.FromSeconds(2), "hints on both lines")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Line 0: "abcX..."
        Assert.Equal("a", snapshot.GetCell(0, 0).Character);
        Assert.Equal("b", snapshot.GetCell(1, 0).Character);
        Assert.Equal("c", snapshot.GetCell(2, 0).Character);
        Assert.Equal("X", snapshot.GetCell(3, 0).Character);

        // Line 1: "defY..."
        Assert.Equal("d", snapshot.GetCell(0, 1).Character);
        Assert.Equal("e", snapshot.GetCell(1, 1).Character);
        Assert.Equal("f", snapshot.GetCell(2, 1).Character);
        Assert.Equal("Y", snapshot.GetCell(3, 1).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_HintAtStartOfLine_PrependedBeforeFirstChar()
    {
        // Hint at column 1 (before 'a') → "Xa"
        var (node, workload, terminal, context, theme) = CreateEditor("ab", 30, 3, focused: false);
        var session = (IEditorSession)node;

        session.PushInlineHints([
            new InlineHint(new DocumentPosition(1, 1), "X")
        ]);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("Xab");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "hint prepended at start of line")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        Assert.Equal("X", snapshot.GetCell(0, 0).Character);
        Assert.Equal("a", snapshot.GetCell(1, 0).Character);
        Assert.Equal("b", snapshot.GetCell(2, 0).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_HintPushedOverViewport_TextTruncatedCorrectly()
    {
        // Viewport only 10 chars wide. "abcde" + hint "XXXXX" at end = "abcdeXXXXX" exactly fills it
        var (node, workload, terminal, context, theme) = CreateEditor("abcde", 10, 3, focused: false);
        var session = (IEditorSession)node;

        session.PushInlineHints([
            new InlineHint(new DocumentPosition(1, 6), "XXXXX")
        ]);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("abcdeXXXXX");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "hint fills viewport exactly")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Verify all 10 characters
        Assert.Equal("a", snapshot.GetCell(0, 0).Character);
        Assert.Equal("e", snapshot.GetCell(4, 0).Character);
        Assert.Equal("X", snapshot.GetCell(5, 0).Character);
        Assert.Equal("X", snapshot.GetCell(9, 0).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_NoHints_RendersNormally()
    {
        // Sanity check: without hints, text renders as expected
        var (node, workload, terminal, context, theme) = CreateEditor("hello world", 30, 3, focused: false);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("hello world");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered without hints")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        Assert.Equal("h", snapshot.GetCell(0, 0).Character);
        Assert.Equal("e", snapshot.GetCell(1, 0).Character);
        Assert.Equal("l", snapshot.GetCell(2, 0).Character);
        Assert.Equal("l", snapshot.GetCell(3, 0).Character);
        Assert.Equal("o", snapshot.GetCell(4, 0).Character);
        Assert.Equal(" ", snapshot.GetCell(5, 0).Character);
        Assert.Equal("w", snapshot.GetCell(6, 0).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_ClearHints_HintsDisappearOnRerender()
    {
        var (node, workload, terminal, context, theme) = CreateEditor("ab", 30, 3, focused: false);
        var session = (IEditorSession)node;

        // First render with hint
        session.PushInlineHints([
            new InlineHint(new DocumentPosition(1, 2), "X")
        ]);

        node.Render(context);

        var pattern1 = new CellPatternSearcher().Find("aXb");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern1).HasMatches,
                TimeSpan.FromSeconds(2), "hint rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Clear hints and re-render
        session.ClearInlineHints();
        node.Render(context);

        var pattern2 = new CellPatternSearcher().Find("ab");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                var result = s.SearchPattern(pattern2);
                if (!result.HasMatches) return false;
                // Verify 'X' is gone - 'b' should be at column 1, not column 2
                var cell = s.GetCell(1, 0);
                return cell.Character == "b";
            },
                TimeSpan.FromSeconds(2), "hints cleared, text back to normal")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        Assert.Equal("a", snapshot.GetCell(0, 0).Character);
        Assert.Equal("b", snapshot.GetCell(1, 0).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_HintWithItalic_ThemeDefaultApplied()
    {
        // Default theme has IsItalic = true for inline hints
        var (node, workload, terminal, context, theme) = CreateEditor("hello", 30, 3, focused: false);
        var session = (IEditorSession)node;

        session.PushInlineHints([
            new InlineHint(new DocumentPosition(1, 6), ": int")
        ]);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("hello: int");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "hint with italic rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Verify hint text has italic attribute (theme default is italic=true)
        var hintIsItalic = theme.Get(InlineHintTheme.IsItalic);
        Assert.True(hintIsItalic, "Default theme should have IsItalic=true for inline hints");

        // Verify hint characters have the italic flag
        for (var x = 5; x <= 9; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.True(cell.IsItalic,
                $"Hint column {x}: expected italic, but cell was not italic");
        }

        // Original text should NOT be italic (no decoration applied)
        for (var x = 0; x <= 4; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.False(cell.IsItalic,
                $"Text column {x}: should not be italic, but cell was italic");
        }

        workload.Dispose();
        terminal.Dispose();
    }
}
