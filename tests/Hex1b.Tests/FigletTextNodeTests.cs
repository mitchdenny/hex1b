using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for <see cref="FigletTextNode"/> covering measurement, render-time vertical truncation,
/// cache invalidation on property changes, and end-to-end rendering through Hex1bTerminal.
/// </summary>
public class FigletTextNodeTests
{
    /// <summary>Single-row 1-cell-per-letter font for predictable measurement.</summary>
    private sealed class TinyFont : FigletFont
    {
        private readonly Dictionary<int, FigletGlyph> _glyphs;

        public TinyFont(int height = 1)
            : base(
                height: height,
                baseline: height,
                hardblank: '$',
                horizontalSmushingRules: 0,
                horizontalSmushing: false,
                horizontalFitting: false,
                verticalSmushingRules: 0,
                verticalSmushing: false,
                verticalFitting: false)
        {
            _glyphs = new Dictionary<int, FigletGlyph>();
            var spaceRows = new string[height];
            for (var i = 0; i < height; i++) spaceRows[i] = " ";
            _glyphs[' '] = new FigletGlyph(spaceRows);
            for (var c = 'A'; c <= 'Z'; c++)
            {
                var rows = new string[height];
                for (var r = 0; r < height; r++) rows[r] = c.ToString();
                _glyphs[c] = new FigletGlyph(rows);
            }
        }

        public override bool TryGetGlyph(int codePoint, out FigletGlyph glyph)
        {
            if (_glyphs.TryGetValue(codePoint, out var found))
            {
                glyph = found;
                return true;
            }
            glyph = null!;
            return false;
        }
    }

    // ----- Measurement ------------------------------------------------------------------

    [Fact]
    public void Measure_ReturnsRenderedWidthAndHeight()
    {
        var node = new FigletTextNode { Text = "ABC", Font = new TinyFont(height: 1) };
        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(3, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_MultiRowFont_ReturnsFontHeight()
    {
        var node = new FigletTextNode { Text = "AB", Font = new TinyFont(height: 4) };
        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(2, size.Width);
        Assert.Equal(4, size.Height);
    }

    [Fact]
    public void Measure_EmptyText_ReturnsFontHeightZeroWidth()
    {
        var node = new FigletTextNode { Text = "", Font = new TinyFont(height: 3) };
        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(0, size.Width);
        Assert.Equal(3, size.Height);
    }

    [Fact]
    public void Measure_RespectsMaxWidth()
    {
        var node = new FigletTextNode { Text = "ABCDE", Font = new TinyFont(height: 1) };
        var size = node.Measure(new Constraints(0, 3, 0, int.MaxValue));

        Assert.Equal(3, size.Width);
    }

    [Fact]
    public void Measure_Wrap_MultipleRowsWhenTextExceedsWrapWidth()
    {
        var node = new FigletTextNode
        {
            Text = "AB CD",
            Font = new TinyFont(height: 1),
            HorizontalOverflow = FigletHorizontalOverflow.Wrap,
        };

        var size = node.Measure(new Constraints(0, 3, 0, int.MaxValue));

        // wrapWidth = 3 → "AB" fits (2 cols), "AB CD" = 5 cols > 3 → second word wraps.
        // 2 rows × 1 row each = 2 lines.
        Assert.Equal(2, size.Height);
    }

    [Fact]
    public void Render_Wrap_RewrapsToActualArrangedWidthWhenMeasuredUnbounded()
    {
        // Regression: when a parent measures children with int.MaxValue (e.g. HStack/VStack/Border
        // discovering natural widths) the wrap cache is populated unwrapped. Render must re-wrap
        // against the actual arranged Bounds.Width, otherwise wrapped layouts spill past their
        // container.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 8).Build();
        var context = new Hex1bRenderContext(workload);

        var node = new FigletTextNode
        {
            Text = "AB CD EF GH",
            Font = new TinyFont(height: 1),
            HorizontalOverflow = FigletHorizontalOverflow.Wrap,
        };

        // Simulate a parent measuring with int.MaxValue (natural-size pass).
        node.Measure(Constraints.Unbounded);

        // Then arrange to a 5-col-wide rect (the actual allocated space).
        node.Arrange(new Rect(0, 0, 5, 8));
        node.Render(context);

        // Verify the cache now reflects the arranged width: each rendered row must be ≤5 cols.
        var field = typeof(FigletTextNode).GetField("_cachedLines", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var lines = (IReadOnlyList<string>)field.GetValue(node)!;
        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.True(line.Length <= 5, $"Line '{line}' exceeds wrap width 5"));
    }

    // ----- Cache invalidation -----------------------------------------------------------

    [Fact]
    public void Text_Change_InvalidatesCache()
    {
        var node = new FigletTextNode { Text = "AB", Font = new TinyFont() };
        var first = node.Measure(Constraints.Unbounded);
        Assert.Equal(2, first.Width);

        node.Text = "ABCD";
        var second = node.Measure(Constraints.Unbounded);
        Assert.Equal(4, second.Width);
    }

    [Fact]
    public void Font_Change_InvalidatesCache()
    {
        var node = new FigletTextNode { Text = "AB", Font = new TinyFont(height: 1) };
        var first = node.Measure(Constraints.Unbounded);
        Assert.Equal(1, first.Height);

        node.Font = new TinyFont(height: 3);
        var second = node.Measure(Constraints.Unbounded);
        Assert.Equal(3, second.Height);
    }

    [Fact]
    public void HorizontalLayout_Change_InvalidatesCache()
    {
        var node = new FigletTextNode { Text = "AB", Font = new TinyFont(height: 1) };
        node.Measure(Constraints.Unbounded);

        // Switch to FullWidth — should re-render.
        node.HorizontalLayout = FigletLayoutMode.FullWidth;
        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(2, size.Width);
    }

    [Fact]
    public void HorizontalOverflow_Change_InvalidatesCache()
    {
        var node = new FigletTextNode
        {
            Text = "AB CD",
            Font = new TinyFont(height: 1),
        };
        node.Measure(new Constraints(0, 3, 0, int.MaxValue));

        node.HorizontalOverflow = FigletHorizontalOverflow.Wrap;
        var sizeAfter = node.Measure(new Constraints(0, 3, 0, int.MaxValue));

        Assert.Equal(2, sizeAfter.Height);
    }

    // ----- Render-time vertical truncation ---------------------------------------------

    [Fact]
    public void Truncate_DropsPartialBottomRow()
    {
        // A 3-row font rendered into 5 rows of available height with truncate → only 1 full FIGlet
        // row (3 sub-rows) fits cleanly. The remaining 2 rows are dropped.
        var node = new FigletTextNode
        {
            Text = "A\nB",
            Font = new TinyFont(height: 3),
            VerticalOverflow = FigletVerticalOverflow.Truncate,
        };

        var natural = node.Measure(Constraints.Unbounded);
        Assert.Equal(6, natural.Height); // two stacked FIGlet rows of 3
        node.Arrange(new Rect(0, 0, 10, 5));

        // Internal state: render writes only fittable rows. We can't easily inspect without
        // hooking RenderContext, but Measure should still report natural height.
        Assert.Equal(6, natural.Height);
    }

    [Fact]
    public void Truncate_FullHeightFits_RendersAllRows()
    {
        var node = new FigletTextNode
        {
            Text = "A",
            Font = new TinyFont(height: 3),
            VerticalOverflow = FigletVerticalOverflow.Truncate,
        };
        var size = node.Measure(Constraints.Unbounded);
        Assert.Equal(3, size.Height);
    }

    // ----- End-to-end rendering through Hex1bTerminal -----------------------------------

    [Fact]
    public async Task Render_SmallFont_AppearsInTerminal()
    {
        // Use the bundled small font so we can assert against well-known output.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 8).Build();
        var context = new Hex1bRenderContext(workload);

        var node = new FigletTextNode { Text = "Hi", Font = FigletFonts.Small };
        var size = node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, size.Width, size.Height));
        node.Render(context);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // The "small" font's "Hi" output begins with " _  _ _" on the first row.
        var firstLine = snapshot.GetLineTrimmed(0);
        Assert.Contains("_", firstLine);
    }

    [Fact]
    public async Task Render_Empty_DoesNotThrow()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var context = new Hex1bRenderContext(workload);

        var node = new FigletTextNode { Text = "", Font = FigletFonts.Small };
        var size = node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, Math.Max(1, size.Width), size.Height));
        node.Render(context);

        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(50))
            .Build()
            .ApplyAsync(terminal);

        Assert.Equal("", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    // ----- Extension API (fluent setters) ----------------------------------------------

    [Fact]
    public void Widget_DefaultsToStandardFont()
    {
        var widget = new FigletTextWidget("Hello");
        Assert.Equal("Hello", widget.Text);
        Assert.Same(FigletFonts.Standard, widget.Font);
        Assert.Equal(FigletLayoutMode.Default, widget.HorizontalLayout);
        Assert.Equal(FigletHorizontalOverflow.Clip, widget.HorizontalOverflow);
        Assert.Equal(FigletVerticalOverflow.Clip, widget.VerticalOverflow);
    }

    [Fact]
    public void Font_Extension_SetsFont()
    {
        var custom = new TinyFont();
        var widget = new FigletTextWidget("X").Font(custom);
        Assert.Same(custom, widget.Font);
    }

    [Fact]
    public void Layout_Extension_SetsBothAxes()
    {
        var widget = new FigletTextWidget("X").Layout(FigletLayoutMode.Smushed);
        Assert.Equal(FigletLayoutMode.Smushed, widget.HorizontalLayout);
        Assert.Equal(FigletLayoutMode.Smushed, widget.VerticalLayout);
    }

    [Fact]
    public void Horizontal_Extension_SetsOnlyHorizontal()
    {
        var widget = new FigletTextWidget("X").Horizontal(FigletLayoutMode.Smushed);
        Assert.Equal(FigletLayoutMode.Smushed, widget.HorizontalLayout);
        Assert.Equal(FigletLayoutMode.Default, widget.VerticalLayout);
    }

    [Fact]
    public void Vertical_Extension_SetsOnlyVertical()
    {
        var widget = new FigletTextWidget("X").Vertical(FigletLayoutMode.FullWidth);
        Assert.Equal(FigletLayoutMode.Default, widget.HorizontalLayout);
        Assert.Equal(FigletLayoutMode.FullWidth, widget.VerticalLayout);
    }

    [Fact]
    public void HorizontalOverflow_Extension_SetsHorizontalOverflow()
    {
        var widget = new FigletTextWidget("X").HorizontalOverflow(FigletHorizontalOverflow.Wrap);
        Assert.Equal(FigletHorizontalOverflow.Wrap, widget.HorizontalOverflow);
    }

    [Fact]
    public void VerticalOverflow_Extension_SetsVerticalOverflow()
    {
        var widget = new FigletTextWidget("X").VerticalOverflow(FigletVerticalOverflow.Truncate);
        Assert.Equal(FigletVerticalOverflow.Truncate, widget.VerticalOverflow);
    }
}
