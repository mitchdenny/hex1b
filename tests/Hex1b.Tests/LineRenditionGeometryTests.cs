using Hex1b.Automation;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class LineRenditionGeometryTests
{
    [TestMethod]
    [DataRow("\x1b[1'}", "A BCD", "V WXYZ")]
    [DataRow("\x1b[1'~", "ACDE", "VXYZ")]
    public void EditColumns_MixedRenditions_KeepsLogicalBoundsAndLinkOwners(string command, string enlarged, string normal)
    {
        using var terminal = Create();
        Feed(terminal, "\x1b#6\x1b]8;;https://example.test/columns\x1b\\ABCDE" +
            "\x1b]8;;\x1b\\\r\nVWXYZ\x1b[1;2H" + command);
        AssertValid(terminal);
        var buffer = terminal.GetScreenBufferSnapshot().Buffer;
        for (var column = 5; column < 10; column++)
            Assert.AreEqual(" ", buffer[0, column].Character, $"Hidden cell {column}");
        using (var snapshot = terminal.CreateSnapshot())
        {
            Assert.AreEqual(enlarged, snapshot.GetLineTrimmed(0));
            Assert.AreEqual(normal, snapshot.GetLineTrimmed(1));
        }
        Assert.AreEqual(4, terminal.GetTrackedHyperlinkAt(0, 0)!.RefCount);
        Assert.AreEqual(1, terminal.CursorX);
        Assert.AreEqual(0, terminal.CursorY);
        Feed(terminal, "\x1b[2J");
        Assert.AreEqual(0, terminal.TrackedHyperlinkCount);
    }

    [TestMethod]
    [DataRow("\x1b[1'}", "A BC")]
    [DataRow("\x1b[1'~", "AC界")]
    public void EditColumns_EnlargedWideGlyphAtEdge_DoesNotLeaveFragments(string command, string expected)
    {
        using var terminal = Create();
        Feed(terminal, "\x1b#6ABC界\x1b[2G" + command);
        AssertValid(terminal);
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(expected, snapshot.GetLineTrimmed(0));
    }

    [TestMethod]
    [DataRow("\x1b[99'}", "A")]
    [DataRow("\x1b[99'~", "A")]
    [DataRow("\x1b[1'}", "A BCD")]
    [DataRow("\x1b[1'~", "ACDE")]
    public void EditColumns_MixedRows_ClampsCountIndependently(string command, string expected)
    {
        using var terminal = Create();
        Feed(terminal, "ABCDEFGHIJ\r\n\x1b#6ABCDE\x1b[H\x1b[2G" + command);
        AssertValid(terminal);
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(expected, snapshot.GetLineTrimmed(1));
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void MoveCursor_MixedRows_UsesDestinationLogicalBounds(char code)
    {
        foreach (var command in new[] { "\x1b[B", "\x1b[2d", "\x1b" + "D", "\n" })
        {
            using var terminal = Create();
            Feed(terminal, $"\x1b[2;1H\x1b#{code}\x1b[1;10H" + command);
            Assert.AreEqual(code == '5' ? 9 : 4, terminal.CursorX, command);
            Assert.AreEqual(1, terminal.CursorY, command);
            AssertValid(terminal);
        }
        foreach (var command in new[] { "\x1b[A", "\x1b[1d", "\x1bM" })
        {
            using var terminal = Create();
            Feed(terminal, $"\x1b#{code}\x1b[2;10H" + command);
            Assert.AreEqual(code == '5' ? 9 : 4, terminal.CursorX, command);
            Assert.AreEqual(0, terminal.CursorY, command);
            AssertValid(terminal);
        }
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void AddressCursor_ColumnsAndTabs_StayWithinLogicalBounds(char code)
    {
        foreach (var command in new[] { "\x1b[999G", "\x1b[999C", "\x1b[1;999H", "\t\t" })
        {
            using var terminal = Create();
            Feed(terminal, $"\x1b#{code}" + command);
            Assert.AreEqual(code == '5' ? 9 : 4, terminal.CursorX, command);
            AssertValid(terminal);
        }
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void RestoreCursor_AfterResize_ClampsSavedRowAndColumn(char code)
    {
        using var terminal = Create();
        Feed(terminal, $"\x1b[4;10H\x1b" + "7\x1b[H" + $"\x1b#{code}");
        terminal.Resize(6, 1);
        Feed(terminal, "\x1b" + "8");
        Assert.AreEqual(0, terminal.CursorY);
        Assert.AreEqual(code == '5' ? 5 : 2, terminal.CursorX);
        AssertValid(terminal);
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void Scroll_ChangesRowUnderStationaryCursor_ReboundsCursorLocally(char code)
    {
        foreach (var down in new[] { false, true })
        {
            using var terminal = Create();
            Feed(terminal, $"\x1b[{(down ? 1 : 3)};1H\x1b#{code}\x1b[2;10H" +
                (down ? "\x1b[T" : "\x1b[S"));
            Assert.AreEqual(1, terminal.CursorY);
            Assert.AreEqual(code == '5' ? 9 : 4, terminal.CursorX);
            AssertValid(terminal);
        }
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void Print_SplitGraphemesAtRightEdge_MatchesWholeGrapheme(char code)
    {
        foreach (var grapheme in new[] { "#\ufe0f", "क्‍ष", "👩‍👦", "e\u0301" })
        {
            using var whole = Create();
            using var split = Create();
            var prefix = $"\x1b#{code}\x1b[{(code == '5' ? 10 : 5)}G";
            Feed(whole, prefix + grapheme + "Z");
            Feed(split, prefix);
            foreach (var rune in grapheme.EnumerateRunes())
            {
                Feed(split, rune.ToString());
                AssertValid(split);
            }
            Feed(split, "Z");
            using var expected = whole.CreateSnapshot();
            using var actual = split.CreateSnapshot();
            Assert.AreEqual(expected.CursorX, actual.CursorX, grapheme);
            Assert.AreEqual(expected.CursorY, actual.CursorY, grapheme);
            for (var row = 0; row < 4; row++)
                for (var column = 0; column < 10; column++)
                    Assert.AreEqual(expected.GetCell(column, row).Character,
                        actual.GetCell(column, row).Character, $"{grapheme} at {column},{row}");
            AssertValid(whole);
            AssertValid(split);
        }
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void WidenGrapheme_AfterPenChange_PreservesStyleAndHyperlink(char code)
    {
        foreach (var (start, suffix) in new[] { ("#", "\ufe0f"), ("क्‍", "ष") })
        {
            using var terminal = Create();
            Feed(terminal, $"\x1b#{code}\x1b[{(code == '5' ? 10 : 5)}G" +
                "\x1b[31;1m\x1b]8;;https://example.test/glyph\x1b\\" + start);
            var original = terminal.GetTrackedHyperlinkAt(code == '5' ? 9 : 4, 0);
            Feed(terminal, "\x1b[0;32m\x1b]8;;\x1b\\" + suffix);
            using (var snapshot = terminal.CreateSnapshot())
            {
                Assert.AreEqual(start + suffix, snapshot.GetCell(0, 1).Character);
                Assert.IsTrue(snapshot.GetCell(0, 1).IsBold);
                Assert.AreSame(original, snapshot.GetCell(0, 1).TrackedHyperlink);
                Assert.AreSame(original, snapshot.GetCell(1, 1).TrackedHyperlink);
            }
            AssertValid(terminal);
            Feed(terminal, "\x1b[2J");
            Assert.AreEqual(0, terminal.TrackedHyperlinkCount);
        }
    }

    [TestMethod]
    public void WidenGrapheme_OneColumnDestination_DoesNotCreateHalfAGlyph()
    {
        using var terminal = Create(width: 3);
        Feed(terminal, "\x1b[2;1H\x1b#6\x1b[1;3H#");
        Feed(terminal, "\ufe0f");
        AssertValid(terminal);
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("", snapshot.GetLineTrimmed(1));
    }

    [TestMethod]
    public void LineFeed_BelowScrollRegion_DoesNotScrollOtherRows()
    {
        using var terminal = Create();
        Feed(terminal, "A\r\nB\r\nC\r\nD\x1b[2;3r\x1b[4;1H\n");
        using var snapshot = terminal.CreateSnapshot();
        TestSeq.AreEqual(new[] { "A", "B", "C", "D" },
            Enumerable.Range(0, 4).Select(snapshot.GetLineTrimmed));
        Assert.AreEqual(3, terminal.CursorY);
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void WidenGrapheme_ScrollRegionBottom_KeepsOutsideRowsIntact(char code)
    {
        foreach (var (start, suffix) in new[] { ("#", "\ufe0f"), ("क्‍", "ष") })
        {
            using var terminal = Create();
            Feed(terminal, "outer\x1b[4;1Hguard\x1b[2;3r" +
                $"\x1b[3;1H\x1b#{code}\x1b[{(code == '5' ? 10 : 5)}G" + start);
            Feed(terminal, suffix);
            using var snapshot = terminal.CreateSnapshot();
            Assert.AreEqual("outer", snapshot.GetLineTrimmed(0));
            Assert.AreEqual(start + suffix, snapshot.GetCell(0, 2).Character);
            Assert.AreEqual("guard", snapshot.GetLineTrimmed(3));
            Assert.AreEqual(2, snapshot.CursorY);
            AssertValid(terminal);
        }
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void ShrinkGrapheme_AtMargin_ReleasesTailAndCancelsDeferredWrap(char code)
    {
        using var terminal = Create();
        var width = code == '5' ? 10 : 5;
        Feed(terminal, $"\x1b#{code}\x1b[{width - 1}G\x1b]8;;https://example.test/glyph\x1b\\☔");
        var link = terminal.GetTrackedHyperlinkAt(width - 2, 0)!;
        Feed(terminal, "\x1b]8;;\x1b\\\ufe0e");
        Assert.AreEqual(1, link.RefCount);
        Feed(terminal, "Z");
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("☔\ufe0e", snapshot.GetCell(width - 2, 0).Character);
        Assert.AreEqual("Z", snapshot.GetCell(width - 1, 0).Character);
        Assert.AreEqual(0, snapshot.CursorY);
        AssertValid(terminal);
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void ReverseWrap_AfterWidePadding_RecognizesTheRowBoundary(char code)
    {
        using var terminal = Create();
        var width = code == '5' ? 10 : 5;
        Feed(terminal, $"\x1b#{code}\x1b[{width}G界\r\x1b[?45h\x1b[D");
        Assert.AreEqual(0, terminal.CursorY);
        Assert.AreEqual(width - 1, terminal.CursorX);
        AssertValid(terminal);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(9)]
    [DataRow(10)]
    [DataRow(11)]
    public void Apply_MixedOperationSequence_PreservesRowGeometryAfterEveryOperation(int width)
    {
        using var terminal = Create(width);
        string[] operations =
        [
            "a", "界", "#", "\ufe0f", "☔", "\ufe0e", "\r", "\n", "\t", "\b",
            "\x1b#3", "\x1b#4", "\x1b#5", "\x1b#6",
            "\x1b[2;1H", "\x1b[4;999H", "\x1b[3G",
            "\x1b[A", "\x1b[B", "\x1b[C", "\x1b[D", "\x1b[Z",
            "\x1b" + "7", "\x1b" + "8", "\x1b[S", "\x1b[T",
            "\x1b[L", "\x1b[M", "\x1b[P", "\x1b[@", "\x1b[X",
            "\x1b[2K", "\x1b[J", "\x1b[1J", "\x1b[2J",
            "\x1b[?1049h", "\x1b[?1049l", "\x1b[?7h", "\x1b[?7l",
            "\x1b[?45h", "\x1b[?45l"
        ];
        var random = new Random(403);
        for (var step = 0; step < 250; step++)
        {
            Feed(terminal, operations[random.Next(operations.Length)]);
            AssertValid(terminal);
            if (step == 125)
            {
                terminal.Resize(Math.Max(1, width / 2), 3);
                AssertValid(terminal);
            }
        }
    }

    [TestMethod]
    [DataRow("#", "\ufe0f")]
    [DataRow("क्‍", "ष")]
    public void WidenGrapheme_DisposalDuringScroll_AbortsAndReleasesTemporaryOwner(string start, string suffix)
    {
        Hex1bTerminal? terminal = null;
        terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless(new TerminalCapabilities { SupportsRetroactiveVariationSelectors = true })
            .WithDimensions(10, 4).WithScrollback(10, _ => terminal!.Dispose()).Build();
        using (terminal)
        {
            Feed(terminal, "\x1b[4;10H\x1b]8;;https://example.test/glyph\x1b\\" +
                start + "\x1b]8;;\x1b\\");
            var link = terminal.GetTrackedHyperlinkAt(9, 3)!;
            Feed(terminal, suffix + "Z\x1b[?1049h");
            // Disposed terminals retain text for snapshots; only the spacer
            // should own a reference, not the temporary grapheme-update owner.
            Assert.AreEqual(1, link.RefCount);
            Assert.AreSame(link, terminal.GetTrackedHyperlinkAt(9, 3));
            using var snapshot = terminal.CreateSnapshot();
            Assert.IsFalse(snapshot.InAlternateScreen);
            Assert.IsFalse(snapshot.ContainsText("Z"));
            Assert.AreEqual(9, snapshot.CursorX);
            Assert.AreEqual(3, snapshot.CursorY);
        }
    }

    private static void AssertValid(Hex1bTerminal terminal)
    {
        using var snapshot = terminal.CreateSnapshot();
        Assert.IsTrue(snapshot.CursorY >= 0 && snapshot.CursorY < snapshot.Height);
        for (var row = 0; row < snapshot.Height; row++)
        {
            var width = snapshot.GetLineRendition(row) == LineRendition.SingleWidth
                ? snapshot.Width : Math.Max(1, snapshot.Width / 2);
            if (snapshot.CursorY == row)
                Assert.IsTrue(snapshot.CursorX >= 0 && snapshot.CursorX < width);
            for (var column = 0; column < width; column++)
            {
                var cell = snapshot.GetCell(column, row);
                if (cell.Character == "")
                {
                    Assert.IsTrue(column > 0);
                    Assert.AreEqual(2, GlyphWidth(snapshot.GetCell(column - 1, row).Character));
                }
                else if (GlyphWidth(cell.Character) > 1)
                {
                    Assert.IsTrue(column + 1 < width, $"wide glyph exceeds row {row}");
                    Assert.AreEqual("", snapshot.GetCell(column + 1, row).Character);
                }
            }
            for (var column = width; column < snapshot.Width; column++)
                Assert.AreEqual(" ", snapshot.GetCell(column, row).Character);
        }
    }

    // Mode 2027 assigns this fixture's Indic cluster two logical cells.
    private static int GlyphWidth(string text) =>
        text == "क्‍ष" ? 2 : DisplayWidth.GetGraphemeWidth(text);

    private static Hex1bTerminal Create(int width = 10) => Hex1bTerminal.CreateBuilder()
        .WithWorkload(new Hex1bAppWorkloadAdapter())
        .WithHeadless(new TerminalCapabilities { SupportsRetroactiveVariationSelectors = true })
        .WithDimensions(width, 4).Build();

    private static void Feed(Hex1bTerminal terminal, string text) =>
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(text));
}
