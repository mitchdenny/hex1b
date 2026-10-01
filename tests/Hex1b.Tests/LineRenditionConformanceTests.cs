using Hex1b.Automation;
using Hex1b.Tokens;

namespace Hex1b.Tests;

// Original scenarios; normative and compatibility references are recorded in
// docs/dec-line-rendition-plan.md. No upstream test implementation is copied.
[TestClass]
public class LineRenditionConformanceTests
{
    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void SetRendition_AllTransitions_TruncateWithoutResurrectingTextAndCancelWrap(char source)
    {
        foreach (var destination in "3456")
        {
            using var terminal = Create();
            Feed(terminal, $"abcdefghij\x1b#{source}\x1b#{destination}Z");
            using var snapshot = terminal.CreateSnapshot();
            var cropped = source != '5' || destination != '5';
            Assert.AreEqual(cropped ? "abcdZ" : "abcdefghiZ", snapshot.GetLineTrimmed(0),
                $"{source} -> {destination}");
            Assert.AreEqual("", snapshot.GetLineTrimmed(1));
            Assert.AreEqual(Rendition(destination), snapshot.GetLineRendition(0));
            Assert.AreEqual(cropped && destination == '5' ? 5 : cropped ? 4 : 9, snapshot.CursorX);
            Assert.AreEqual(0, snapshot.CursorY);
        }
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void Print_AllDestinationModes_WrapUsesDestinationLogicalWidth(char source)
    {
        foreach (var destination in "3456")
        {
            using var terminal = Create();
            var sourceWidth = source == '5' ? 10 : 5;
            var destinationWidth = destination == '5' ? 10 : 5;
            Feed(terminal, $"\x1b[2;1H\x1b#{destination}\x1b[H\x1b#{source}" +
                new string('a', sourceWidth) + new string('b', destinationWidth) + "c");
            using var snapshot = terminal.CreateSnapshot();
            Assert.AreEqual(new string('a', sourceWidth), snapshot.GetLineTrimmed(0));
            Assert.AreEqual(new string('b', destinationWidth), snapshot.GetLineTrimmed(1));
            Assert.AreEqual("c", snapshot.GetLineTrimmed(2));
            Assert.AreEqual(Rendition(source), snapshot.GetLineRendition(0));
            Assert.AreEqual(Rendition(destination), snapshot.GetLineRendition(1));
            Assert.AreEqual(LineRendition.SingleWidth, snapshot.GetLineRendition(2));
            Assert.AreEqual(1, snapshot.CursorX);
            Assert.AreEqual(2, snapshot.CursorY);
        }
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void EditCharacters_AllRenditions_StayInsideLogicalRow(char code)
    {
        using var terminal = Create();
        var content = code == '5' ? "ABCDEFGHIJ" : "ABCDE";
        Feed(terminal, $"\x1b#{code}{content}\r\x1b[2G\x1b[2P");
        using (var deleted = terminal.CreateSnapshot())
            Assert.AreEqual(code == '5' ? "ADEFGHIJ" : "ADE", deleted.GetLineTrimmed(0));
        Feed(terminal, "\x1b[2@");
        using (var inserted = terminal.CreateSnapshot())
            Assert.AreEqual(code == '5' ? "A  DEFGHIJ" : "A  DE", inserted.GetLineTrimmed(0));
        Feed(terminal, "\x1b[3G\x1b[99X");
        using var erased = terminal.CreateSnapshot();
        Assert.AreEqual("A", erased.GetLineTrimmed(0));
        Assert.AreEqual(Rendition(code), erased.GetLineRendition(0));
        Assert.AreEqual(2, erased.CursorX);
        Assert.AreEqual("", erased.GetLineTrimmed(1));
        AssertHiddenCellsEmpty(erased, 0);
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void Repeat_AutowrapDisabled_NeverAdvancesToNextRow(char code)
    {
        using var terminal = Create();
        var width = code == '5' ? 10 : 5;
        Feed(terminal, $"\x1b#{code}\x1b[?7la\x1b[20b");
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(new string('a', width), snapshot.GetLineTrimmed(0));
        Assert.AreEqual("", snapshot.GetLineTrimmed(1));
        Assert.AreEqual(width - 1, snapshot.CursorX);
        Assert.AreEqual(0, snapshot.CursorY);
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void Repeat_WideGlyphAtRightEdge_WrapsBeforeWriting(char code)
    {
        using var terminal = Create();
        var width = code == '5' ? 10 : 5;
        Feed(terminal, $"\x1b#{code}界\x1b[{width}G\x1b[b");
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("界", snapshot.GetLineTrimmed(0));
        Assert.AreEqual("界", snapshot.GetLineTrimmed(1));
        Assert.AreEqual(2, snapshot.CursorX);
        Assert.AreEqual(1, snapshot.CursorY);
        AssertHiddenCellsEmpty(snapshot, 0);
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void Print_AtBottomOfScrollRegion_ScrollsRowsAndTheirRenditions(char code)
    {
        using var terminal = Create();
        var width = code == '5' ? 10 : 5;
        Feed(terminal, "\x1b[Houter\x1b[4;1Hguard\x1b[2;3r" +
            $"\x1b[3;1H\x1b#{code}" + new string('a', width) + "b");
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("outer", snapshot.GetLineTrimmed(0));
        Assert.AreEqual(new string('a', width), snapshot.GetLineTrimmed(1));
        Assert.AreEqual(Rendition(code), snapshot.GetLineRendition(1));
        Assert.AreEqual("b", snapshot.GetLineTrimmed(2));
        Assert.AreEqual(LineRendition.SingleWidth, snapshot.GetLineRendition(2));
        Assert.AreEqual("guard", snapshot.GetLineTrimmed(3));
        Assert.AreEqual(2, snapshot.CursorY);
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void Repeat_AllDestinationModes_UsesDestinationLogicalWidth(char source)
    {
        foreach (var destination in "3456")
        {
            using var terminal = Create();
            var sourceWidth = source == '5' ? 10 : 5;
            var destinationWidth = destination == '5' ? 10 : 5;
            Feed(terminal, $"\x1b[2;1H\x1b#{destination}\x1b[H\x1b#{source}" +
                $"a\x1b[{sourceWidth + destinationWidth}b");
            using var snapshot = terminal.CreateSnapshot();
            Assert.AreEqual(new string('a', sourceWidth), snapshot.GetLineTrimmed(0));
            Assert.AreEqual(new string('a', destinationWidth), snapshot.GetLineTrimmed(1));
            Assert.AreEqual("a", snapshot.GetLineTrimmed(2));
            Assert.AreEqual(1, snapshot.CursorX);
            Assert.AreEqual(2, snapshot.CursorY);
            AssertHiddenCellsEmpty(snapshot, 0);
            AssertHiddenCellsEmpty(snapshot, 1);
        }
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void PrintWide_AtBottomOfScrollRegion_PrintAndRepeatPreserveOutsideRows(char code)
    {
        foreach (var output in new[] { "界", "\x1b[b" })
        {
            using var terminal = Create();
            var width = code == '5' ? 10 : 5;
            Feed(terminal, "\x1b[Houter\x1b[4;1Hguard\x1b[2;3r" +
                $"\x1b[3;1H\x1b#{code}界\x1b[{width}G" + output);
            using var snapshot = terminal.CreateSnapshot();
            Assert.AreEqual("outer", snapshot.GetLineTrimmed(0));
            Assert.AreEqual("界", snapshot.GetLineTrimmed(1));
            Assert.AreEqual(Rendition(code), snapshot.GetLineRendition(1));
            Assert.AreEqual("界", snapshot.GetLineTrimmed(2));
            Assert.AreEqual("", snapshot.GetCell(1, 2).Character);
            Assert.AreEqual(LineRendition.SingleWidth, snapshot.GetLineRendition(2));
            Assert.AreEqual("guard", snapshot.GetLineTrimmed(3));
            Assert.AreEqual(2, snapshot.CursorX);
            Assert.AreEqual(2, snapshot.CursorY);
            AssertHiddenCellsEmpty(snapshot, 1);
        }
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void Print_BelowScrollRegion_PrintAndRepeatDoNotScrollUnrelatedRows(char code)
    {
        foreach (var output in new[] { "a", "\x1b[b" })
        {
            using var terminal = Create();
            var width = code == '5' ? 10 : 5;
            Feed(terminal, "outer\r\nfirst\r\nlast\x1b[2;3r" +
                $"\x1b[4;1H\x1b#{code}" + new string('a', width) + output);
            using var snapshot = terminal.CreateSnapshot();
            Assert.AreEqual("outer", snapshot.GetLineTrimmed(0));
            Assert.AreEqual("first", snapshot.GetLineTrimmed(1));
            Assert.AreEqual("last", snapshot.GetLineTrimmed(2));
            Assert.AreEqual(new string('a', width), snapshot.GetLineTrimmed(3));
            Assert.AreEqual(Rendition(code), snapshot.GetLineRendition(3));
            Assert.AreEqual(1, snapshot.CursorX);
            Assert.AreEqual(3, snapshot.CursorY);
            AssertHiddenCellsEmpty(snapshot, 3);
        }
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void Repeat_WideGlyphWithoutAutowrap_DropsGlyphThatDoesNotFit(char code)
    {
        using var terminal = Create();
        var width = code == '5' ? 10 : 5;
        Feed(terminal, $"\x1b#{code}界\x1b[?7l\x1b[{width}G\x1b[3b");
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("界", snapshot.GetLineTrimmed(0));
        Assert.AreEqual("", snapshot.GetLineTrimmed(1));
        Assert.AreEqual(width - 1, snapshot.CursorX);
        Assert.AreEqual(0, snapshot.CursorY);
        AssertHiddenCellsEmpty(snapshot, 0);
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('6')]
    public void PrintWide_DestinationHasOneLogicalColumn_PrintAndRepeatDropUnrepresentableGlyph(char code)
    {
        foreach (var output in new[] { "界", "\x1b[b" })
        {
            using var terminal = Hex1bTerminal.CreateBuilder()
                .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless()
                .WithDimensions(3, 4).Build();
            Feed(terminal, $"\x1b[2;1H\x1b#{code}\x1b[H界\x1b[3G" + output);
            using (var snapshot = terminal.CreateSnapshot())
            {
                Assert.AreEqual("界", snapshot.GetLineTrimmed(0));
                Assert.AreEqual("", snapshot.GetLineTrimmed(1));
                Assert.AreEqual(Rendition(code), snapshot.GetLineRendition(1));
                Assert.AreEqual(0, snapshot.CursorX);
                Assert.AreEqual(1, snapshot.CursorY);
                AssertHiddenCellsEmpty(snapshot, 1);
            }
            Feed(terminal, "X");
            using var continued = terminal.CreateSnapshot();
            Assert.AreEqual("X", continued.GetLineTrimmed(2));
            Assert.AreEqual(1, continued.CursorX);
            Assert.AreEqual(2, continued.CursorY);
        }
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void Repeat_AfterPenChange_PreservesPriorStyleWithoutCopyingHyperlinks(char code)
    {
        using var terminal = Create();
        var width = code == '5' ? 10 : 5;
        Feed(terminal, $"\x1b#{code}\x1b[31;1;4m\x1b]8;;https://example.test/source\x1b\\界");
        using var original = terminal.CreateSnapshot();
        var source = original.GetCell(0, 0);
        Assert.IsNotNull(source.TrackedHyperlink);
        Feed(terminal, $"\x1b[0;32m\x1b]8;;https://example.test/other\x1b\\\x1b[{width}G\x1b[b");
        using var snapshot = terminal.CreateSnapshot();
        var repeated = snapshot.GetCell(0, 1);
        Assert.AreEqual("界", repeated.Character);
        Assert.AreEqual(source.Foreground, repeated.Foreground);
        Assert.AreEqual(source.Attributes, repeated.Attributes);
        Assert.AreEqual(source.UnderlineStyle, repeated.UnderlineStyle);
        Assert.IsNull(repeated.TrackedHyperlink);
        Assert.IsNull(snapshot.GetCell(1, 1).TrackedHyperlink);
        Assert.IsNull(snapshot.GetCell(width - 1, 0).TrackedHyperlink);
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void Print_OverlappingWideGlyph_PrintAndRepeatClearOrphanedCells(char code)
    {
        foreach (var output in new[] { "界", "\x1b[b" })
        {
            using var terminal = Create();
            Feed(terminal, $"\x1b#{code}\x1b[2G界\x1b[2;1H界\x1b[H" + output);
            using var snapshot = terminal.CreateSnapshot();
            Assert.AreEqual("界", snapshot.GetCell(0, 0).Character);
            Assert.AreEqual("", snapshot.GetCell(1, 0).Character);
            Assert.AreEqual(" ", snapshot.GetCell(2, 0).Character);
            Assert.AreEqual(2, snapshot.CursorX);
            Assert.AreEqual(0, snapshot.CursorY);
            AssertHiddenCellsEmpty(snapshot, 0);
        }
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    public void Repeat_InsertMode_ShiftsCharactersWithinLogicalRow(char code)
    {
        using var terminal = Create();
        Feed(terminal, $"\x1b#{code}ABCDE\x1b[2;1HX\x1b[H\x1b[4h\x1b[2b");
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(code == '5' ? "XXABCDE" : "XXABC", snapshot.GetLineTrimmed(0));
        Assert.AreEqual("X", snapshot.GetLineTrimmed(1));
        Assert.AreEqual(2, snapshot.CursorX);
        Assert.AreEqual(0, snapshot.CursorY);
        AssertHiddenCellsEmpty(snapshot, 0);
    }

    [TestMethod]
    [DataRow("\x1b[2S", "C", "D", "", "", LineRendition.DoubleWidth, LineRendition.SingleWidth)]
    [DataRow("\x1b[2T", "", "", "A", "B", LineRendition.SingleWidth, LineRendition.SingleWidth)]
    [DataRow("\x1b[2;1H\x1b[2L", "A", "", "", "B", LineRendition.DoubleHeightTop, LineRendition.SingleWidth)]
    [DataRow("\x1b[2;1H\x1b[2M", "A", "D", "", "", LineRendition.DoubleHeightTop, LineRendition.SingleWidth)]
    public void MoveRows_MultipleRows_PreservesIndependentRenditions(
        string command, string first, string second, string third, string fourth,
        LineRendition firstRendition, LineRendition secondRendition)
    {
        using var terminal = Create();
        Feed(terminal, "\x1b#3A\r\n\x1b#4B\r\n\x1b#6C\r\nD" + command);
        using var snapshot = terminal.CreateSnapshot();
        TestSeq.AreEqual(new[] { first, second, third, fourth },
            Enumerable.Range(0, 4).Select(snapshot.GetLineTrimmed));
        Assert.AreEqual(firstRendition, snapshot.GetLineRendition(0));
        Assert.AreEqual(secondRendition, snapshot.GetLineRendition(1));
        Assert.AreEqual(third == "A" ? LineRendition.DoubleHeightTop : LineRendition.SingleWidth, snapshot.GetLineRendition(2));
        Assert.AreEqual(fourth == "B" ? LineRendition.DoubleHeightBottom : LineRendition.SingleWidth, snapshot.GetLineRendition(3));
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('6')]
    public void AlternateScreen_ResizeAndReturn_TruncatesMainRowsWithoutLosingRendition(char code)
    {
        using var terminal = Create();
        Feed(terminal, $"\x1b#{code}ABCDE\x1b[?1049h");
        terminal.Resize(6, 4);
        Feed(terminal, "\x1b#6alt\x1b[?1049l");
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("ABC", snapshot.GetLineTrimmed(0));
        Assert.AreEqual(Rendition(code), snapshot.GetLineRendition(0));
        Assert.AreEqual(2, snapshot.CursorX);
        AssertHiddenCellsEmpty(snapshot, 0);
    }

    private static void AssertHiddenCellsEmpty(Hex1bTerminalSnapshot snapshot, int row)
    {
        if (snapshot.GetLineRendition(row) == LineRendition.SingleWidth)
            return;
        for (var column = Math.Max(1, snapshot.Width / 2); column < snapshot.Width; column++)
            Assert.AreEqual(" ", snapshot.GetCell(column, row).Character, $"hidden cell ({column},{row})");
    }

    private static LineRendition Rendition(char code) => code switch
    {
        '3' => LineRendition.DoubleHeightTop,
        '4' => LineRendition.DoubleHeightBottom,
        '5' => LineRendition.SingleWidth,
        '6' => LineRendition.DoubleWidth,
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    };

    private static Hex1bTerminal Create() => Hex1bTerminal.CreateBuilder()
        .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless()
        .WithDimensions(10, 4).WithScrollback(20).Build();

    private static void Feed(Hex1bTerminal terminal, string text) =>
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(text));
}
