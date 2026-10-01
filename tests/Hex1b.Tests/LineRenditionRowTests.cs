using Hex1b.Reflow;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class LineRenditionRowTests
{
    [TestMethod]
    [DataRow("\x1b[S", "BCD ")]
    [DataRow("\x1b[T", " ABC")]
    [DataRow("\x1b[L", " ABC")]
    [DataRow("\x1b[M", "BCD ")]
    [DataRow("\x1b[2S", "CD  ")]
    [DataRow("\x1b[2T", "  AB")]
    [DataRow("\x1b[2L", "  AB")]
    [DataRow("\x1b[2M", "CD  ")]
    [DataRow("\x1b[2;3r\x1b[2;1H\x1b[S", "AC D")]
    [DataRow("\x1b[2;3r\x1b[2;1H\x1b[T", "A BD")]
    [DataRow("\x1b[2;3r\x1b[2;1H\x1b[L", "A BD")]
    [DataRow("\x1b[2;3r\x1b[2;1H\x1b[M", "AC D")]
    public void MoveRows_RenditionsAndHyperlinks_TravelTogether(string command, string expected)
    {
        using var terminal = Create();
        Seed(terminal);
        Assert.AreEqual(4, terminal.TrackedHyperlinkCount);
        Feed(terminal, command);
        Assert.AreEqual(expected.Count(c => c != ' '), terminal.TrackedHyperlinkCount);
        using (var snapshot = terminal.CreateSnapshot())
        {
            for (var row = 0; row < 4; row++)
            {
                var character = expected[row];
                Assert.AreEqual(character == ' ' ? "" : character.ToString(), snapshot.GetLineTrimmed(row));
                Assert.AreEqual(Rendition(character), snapshot.GetLineRendition(row));
                if (character != ' ')
                    Assert.IsNotNull(snapshot.GetCell(0, row).TrackedHyperlink);
            }
        }
        for (var row = 0; row < 4; row++)
            if (expected[row] != ' ')
                Assert.AreEqual(1, terminal.GetTrackedHyperlinkAt(0, row)!.RefCount);
        Feed(terminal, "\x1b[2J");
        Assert.AreEqual(0, terminal.TrackedHyperlinkCount);
    }

    [TestMethod]
    [DataRow("\x1b[2J", "")]
    [DataRow("\x1b#8", "EEEEEEEEEE")]
    [DataRow("\x1b" + "c", "")]
    public void ResetRows_WholeScreen_ResetsRenditionAndReleasesLinks(string command, string text)
    {
        using var terminal = Create();
        Seed(terminal);
        Feed(terminal, command);
        Assert.AreEqual(0, terminal.TrackedHyperlinkCount);
        using var snapshot = terminal.CreateSnapshot();
        for (var row = 0; row < 4; row++)
        {
            Assert.AreEqual(text, snapshot.GetLineTrimmed(row));
            Assert.AreEqual(LineRendition.SingleWidth, snapshot.GetLineRendition(row));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AlternateScreen_ResizeAndRestore_RetainsIndependentRowsAndLinks(bool reflow)
    {
        using var terminal = Create(reflow);
        Seed(terminal);
        Feed(terminal, "\x1b[?1049h\x1b[H\x1b#6alternate");
        terminal.Resize(6, 5);
        Feed(terminal, "\x1b[?1049l");
        Assert.AreEqual(4, terminal.TrackedHyperlinkCount);
        using (var snapshot = terminal.CreateSnapshot())
        {
            for (var row = 0; row < 4; row++)
            {
                Assert.AreEqual(((char)('A' + row)).ToString(), snapshot.GetLineTrimmed(row));
                Assert.AreEqual(Rendition((char)('A' + row)), snapshot.GetLineRendition(row));
            }
            Assert.AreEqual(LineRendition.SingleWidth, snapshot.GetLineRendition(4));
            Assert.AreEqual("", snapshot.GetLineTrimmed(4));
        }
        Feed(terminal, "\x1b[2J");
        Assert.AreEqual(0, terminal.TrackedHyperlinkCount);
    }

    [TestMethod]
    [DataRow('3', '4')]
    [DataRow('6', '5')]
    [DataRow('5', '6')]
    public void AlternateScreen_UnchangedSize_PreservesMixedSoftWrap(char first, char second)
    {
        using var terminal = Create();
        Feed(terminal, $"\x1b[2;1H\x1b#{second}\x1b[H\x1b#{first}" +
            new string('A', first == '5' ? 10 : 5) + "Z");
        using (var before = terminal.CreateSnapshot())
            Assert.IsTrue(before.IsLineSoftWrapped(0));
        Feed(terminal, "\x1b[?1049h\x1b#6alternate\x1b[?1049l");
        using var after = terminal.CreateSnapshot();
        Assert.IsTrue(after.IsLineSoftWrapped(0));
        Assert.AreEqual("Z", after.GetLineTrimmed(1));
    }

    [TestMethod]
    [DataRow(false, 3)]
    [DataRow(false, 5)]
    [DataRow(true, 3)]
    [DataRow(true, 5)]
    public void Resize_HeightOnlyWithoutReflow_PreservesEnlargedSoftWrap(bool provider, int height)
    {
        var builder = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(10, 4);
        if (provider)
            builder.WithReflow(NoReflowStrategy.Instance);
        using var terminal = builder.Build();
        Feed(terminal, "\x1b#6ABCDEZ");
        using (var before = terminal.CreateSnapshot())
            Assert.IsTrue(before.IsLineSoftWrapped(0));
        terminal.Resize(10, height);
        using var after = terminal.CreateSnapshot();
        Assert.IsTrue(after.IsLineSoftWrapped(0));
        Assert.AreEqual(LineRendition.DoubleWidth, after.GetLineRendition(0));
        Assert.AreEqual("ABCDE", after.GetLineTrimmed(0));
        Assert.AreEqual("Z", after.GetLineTrimmed(1));
    }

    [TestMethod]
    public void Scroll_HistoryEviction_RetainsOnlyReachableRowsAndLinks()
    {
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(10, 4).WithScrollback(2).Build();
        Seed(terminal);
        Feed(terminal, "\x1b[2S");
        Assert.AreEqual(4, terminal.TrackedHyperlinkCount);
        using (var snapshot = terminal.CreateSnapshot(scrollbackLines: 2))
        {
            for (var row = 0; row < 4; row++)
            {
                var character = (char)('A' + row);
                Assert.AreEqual(character.ToString(), snapshot.GetLineTrimmed(row));
                Assert.AreEqual(Rendition(character), snapshot.GetLineRendition(row));
                Assert.IsNotNull(snapshot.GetCell(0, row).TrackedHyperlink);
            }
        }
        Feed(terminal, "\x1b[S");
        Assert.AreEqual(3, terminal.TrackedHyperlinkCount);
        using (var snapshot = terminal.CreateSnapshot(scrollbackLines: 2))
        {
            Assert.AreEqual("B", snapshot.GetLineTrimmed(0));
            Assert.AreEqual(LineRendition.DoubleHeightBottom, snapshot.GetLineRendition(0));
            Assert.AreEqual("C", snapshot.GetLineTrimmed(1));
            Assert.AreEqual(LineRendition.DoubleWidth, snapshot.GetLineRendition(1));
            Assert.AreEqual("D", snapshot.GetLineTrimmed(2));
        }
        Feed(terminal, "\x1b[3J");
        Assert.AreEqual(0, terminal.TrackedHyperlinkCount);
    }

    [TestMethod]
    [DataRow("\x1b[2;1H\x1b[J", true, false)]
    [DataRow("\x1b[2;2H\x1b[J", false, false)]
    [DataRow("\x1b[2;5H\x1b[1J", true, true)]
    [DataRow("\x1b[2;4H\x1b[1J", false, true)]
    public void EraseDisplay_PartialRow_OnlyNormalizesCompletelyErasedRows(
        string command, bool normalizeCursorRow, bool toStart)
    {
        using var terminal = Create();
        Seed(terminal);
        Feed(terminal, command);
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(toStart ? LineRendition.SingleWidth : LineRendition.DoubleHeightTop,
            snapshot.GetLineRendition(0));
        Assert.AreEqual(normalizeCursorRow ? LineRendition.SingleWidth : LineRendition.DoubleHeightBottom,
            snapshot.GetLineRendition(1));
        Assert.AreEqual(toStart ? LineRendition.DoubleWidth : LineRendition.SingleWidth,
            snapshot.GetLineRendition(2));
    }

    [TestMethod]
    [DataRow("\x1b[?2J")]
    [DataRow("\x1b[?J")]
    [DataRow("\x1b[?1J")]
    public void EraseDisplay_Selective_PreservesProtectedTextAndRendition(string command)
    {
        using var terminal = Create();
        Feed(terminal, "\x1b#3\x1b[1\"qA\x1b[0\"qB\x1b[H" + command);
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("A", snapshot.GetCell(0, 0).Character);
        Assert.AreEqual(LineRendition.DoubleHeightTop, snapshot.GetLineRendition(0));
    }

    [TestMethod]
    public void Snapshot_RowMovementAndResize_DoesNotMutateCapturedRows()
    {
        using var terminal = Create();
        Seed(terminal);
        using (var before = terminal.CreateSnapshot())
        {
            Feed(terminal, "\x1b[S");
            terminal.Resize(6, 4);
            for (var row = 0; row < 4; row++)
            {
                var character = (char)('A' + row);
                Assert.AreEqual(character.ToString(), before.GetLineTrimmed(row));
                Assert.AreEqual(Rendition(character), before.GetLineRendition(row));
            }
        }
        Assert.AreEqual(3, terminal.TrackedHyperlinkCount);
        Feed(terminal, "\x1b[2J");
        Assert.AreEqual(0, terminal.TrackedHyperlinkCount);
    }

    private static LineRendition Rendition(char character) => character switch
    {
        'A' => LineRendition.DoubleHeightTop,
        'B' => LineRendition.DoubleHeightBottom,
        'C' => LineRendition.DoubleWidth,
        _ => LineRendition.SingleWidth
    };

    private static void Seed(Hex1bTerminal terminal)
    {
        var codes = "3465";
        for (var row = 0; row < 4; row++)
            Feed(terminal, $"\x1b[{row + 1};1H\x1b#{codes[row]}" +
                $"\x1b]8;;https://example.test/{row}\x1b\\{(char)('A' + row)}\x1b]8;;\x1b\\");
        Feed(terminal, "\x1b[H");
    }

    private static Hex1bTerminal Create(bool reflow = false)
    {
        var builder = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(10, 4);
        if (reflow)
            builder.WithReflow(GhosttyReflowStrategy.Instance);
        return builder.Build();
    }

    private static void Feed(Hex1bTerminal terminal, string text) =>
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(text));
}
