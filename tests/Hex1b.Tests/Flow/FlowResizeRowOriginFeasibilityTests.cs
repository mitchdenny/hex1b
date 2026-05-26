using Hex1b.Flow;

namespace Hex1b.Tests.Flow;

/// <summary>
/// Feasibility tests for the resize-handler design described in
/// <c>plan.md</c>: confirms that <see cref="FlowResizeMath.ComputeRowOriginAtWidth"/>
/// correctly predicts where the top of the active step will land on screen
/// after the host terminal reflows the tombstones above it at a new width.
/// <para/>
/// The whole "track-and-clear on every event, repaint on settle" design
/// hinges on this primitive being correct. If these tests fail, none of the
/// downstream debounce / placeholder / marker work matters — we'd need to
/// fall back to a CPR-based approach instead.
/// <para/>
/// We validate the math against a separate reference simulator
/// (<see cref="FakeReflowTerminal"/>) that performs reflow on a true 2D grid
/// by writing characters cell-by-cell. That gives an independent oracle:
/// the formula and the simulator can't drift in lockstep because the
/// simulator never calls the formula.
/// </summary>
public class FlowResizeRowOriginFeasibilityTests
{
    [Fact]
    public void NoTombstones_RowOriginEqualsInitial()
    {
        var sim = new FakeReflowTerminal(width: 80, initialCursorRow: 0);
        sim.MarkActiveWidgetTop();

        var actual = ComputeOrigin(initial: 0, sim, width: 80);
        Assert.Equal(sim.ActiveWidgetRow, actual);
        Assert.Equal(0, actual);
    }

    [Fact]
    public void ShortTombstone_FitsAtBothWidths()
    {
        var sim = new FakeReflowTerminal(width: 80, initialCursorRow: 0);
        sim.WriteParagraph("hello");
        sim.MarkActiveWidgetTop();

        foreach (var width in new[] { 80, 40, 20, 10, 6 })
        {
            sim.Resize(width);
            var math = ComputeOrigin(initial: 0, sim, width);
            Assert.Equal(sim.ActiveWidgetRow, math);
            Assert.Equal(1, math);
        }
    }

    [Fact]
    public void LongSingleParagraph_WrapsWhenWidthShrinks()
    {
        var sim = new FakeReflowTerminal(width: 80, initialCursorRow: 0);
        sim.WriteParagraph(new string('x', 60));
        sim.MarkActiveWidgetTop();

        sim.Resize(80);
        Assert.Equal(1, sim.ActiveWidgetRow);
        Assert.Equal(1, ComputeOrigin(0, sim, 80));

        sim.Resize(40);
        Assert.Equal(2, sim.ActiveWidgetRow);
        Assert.Equal(2, ComputeOrigin(0, sim, 40));

        sim.Resize(20);
        Assert.Equal(3, sim.ActiveWidgetRow);
        Assert.Equal(3, ComputeOrigin(0, sim, 20));
    }

    [Fact]
    public void MultiParagraph_MixedWidths()
    {
        var sim = new FakeReflowTerminal(width: 40, initialCursorRow: 0);
        sim.WriteParagraph(new string('a', 10));
        sim.WriteParagraph(new string('b', 60));
        sim.WriteParagraph(new string('c', 5));
        sim.MarkActiveWidgetTop();

        Assert.Equal(4, sim.ActiveWidgetRow); // 1 + 2 + 1
        Assert.Equal(sim.ActiveWidgetRow, ComputeOrigin(0, sim, 40));
    }

    [Fact]
    public void DragResizeSequence_OriginTracksSimulator()
    {
        var sim = new FakeReflowTerminal(width: 80, initialCursorRow: 0);
        // Three paragraphs of varying widths.
        sim.WriteParagraph(new string('a', 35));
        sim.WriteParagraph(new string('b', 70));
        sim.WriteParagraph(new string('c', 12));
        sim.MarkActiveWidgetTop();

        foreach (var width in new[] { 80, 70, 60, 50, 40, 30, 25, 20, 15, 10, 30, 80 })
        {
            sim.Resize(width);
            Assert.Equal(sim.ActiveWidgetRow, ComputeOrigin(0, sim, width));
        }
    }

    [Fact]
    public void WidthGrows_PreviouslyWrappedParagraphUnwraps()
    {
        var sim = new FakeReflowTerminal(width: 40, initialCursorRow: 0);
        sim.WriteParagraph(new string('x', 60));
        sim.MarkActiveWidgetTop();

        sim.Resize(40);
        Assert.Equal(2, sim.ActiveWidgetRow);
        Assert.Equal(2, ComputeOrigin(0, sim, 40));

        sim.Resize(80);
        Assert.Equal(1, sim.ActiveWidgetRow);
        Assert.Equal(1, ComputeOrigin(0, sim, 80));
    }

    [Fact]
    public void RandomWalk_NoCumulativeDrift()
    {
        // Deterministic seed so the test is reproducible.
        var rng = new Random(0xBEEF);
        var sim = new FakeReflowTerminal(width: 80, initialCursorRow: 5);
        for (var i = 0; i < 7; i++)
        {
            sim.WriteParagraph(new string('p', rng.Next(0, 200)));
        }
        sim.MarkActiveWidgetTop();

        for (var step = 0; step < 50; step++)
        {
            var width = rng.Next(4, 200);
            sim.Resize(width);
            var simRow = sim.ActiveWidgetRow;
            var mathRow = ComputeOrigin(initial: 5, sim, width);
            Assert.True(
                simRow == mathRow,
                $"step={step} width={width} sim={simRow} math={mathRow}");
        }
    }

    [Fact]
    public void EmptyParagraph_StillTakesOneRow()
    {
        var sim = new FakeReflowTerminal(width: 40, initialCursorRow: 0);
        sim.WriteParagraph("");
        sim.MarkActiveWidgetTop();

        Assert.Equal(1, sim.ActiveWidgetRow);
        Assert.Equal(1, ComputeOrigin(0, sim, 40));
    }

    [Fact]
    public void ParagraphExactlyTerminalWidth_DoesNotForceExtraRow()
    {
        var sim = new FakeReflowTerminal(width: 40, initialCursorRow: 0);
        sim.WriteParagraph(new string('x', 40));
        sim.MarkActiveWidgetTop();

        Assert.Equal(1, sim.ActiveWidgetRow);
        Assert.Equal(1, ComputeOrigin(0, sim, 40));
    }

    private static int ComputeOrigin(int initial, FakeReflowTerminal sim, int width)
    {
        return FlowResizeMath.ComputeRowOriginAtWidth(
            initial,
            sim.TombstoneParagraphWidths,
            width);
    }

    /// <summary>
    /// Reference reflow simulator: writes characters into an infinite 2D
    /// grid, wrapping at the current terminal width, and treats CR+LF as a
    /// hard paragraph boundary (the next character starts on a fresh row).
    /// Mirrors the wrap behaviour every modern emulator surveyed in the
    /// resize research uses for hard-newline-terminated paragraphs.
    /// </summary>
    private sealed class FakeReflowTerminal
    {
        private readonly List<string> _paragraphs = new();
        private readonly int _initialCursorRow;
        private int _markedParagraphCount = -1;

        public FakeReflowTerminal(int width, int initialCursorRow)
        {
            Width = width;
            _initialCursorRow = initialCursorRow;
        }

        public int Width { get; private set; }

        public void WriteParagraph(string text) => _paragraphs.Add(text);

        public void MarkActiveWidgetTop() => _markedParagraphCount = _paragraphs.Count;

        public void Resize(int width)
        {
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
            Width = width;
        }

        public IReadOnlyList<IReadOnlyList<int>> TombstoneParagraphWidths =>
            _paragraphs
                .Take(_markedParagraphCount < 0 ? _paragraphs.Count : _markedParagraphCount)
                .Select(p => (IReadOnlyList<int>)new[] { p.Length })
                .ToList();

        /// <summary>
        /// Independently computes the display row of the active widget by
        /// simulating cell-by-cell writes into a width-bounded grid, then
        /// returning the cursor row after the last paragraph above the
        /// active widget. Crucially does NOT call ComputeRowOriginAtWidth.
        /// </summary>
        public int ActiveWidgetRow
        {
            get
            {
                var row = _initialCursorRow;
                var col = 0;
                var stop = _markedParagraphCount < 0 ? _paragraphs.Count : _markedParagraphCount;

                for (var i = 0; i < stop; i++)
                {
                    var text = _paragraphs[i];
                    foreach (var _ in text)
                    {
                        if (col == Width)
                        {
                            row++;
                            col = 0;
                        }
                        col++;
                    }
                    // Hard newline: paragraph boundary always advances to a fresh row,
                    // regardless of whether the current row had content.
                    row++;
                    col = 0;
                }
                return row;
            }
        }
    }
}
