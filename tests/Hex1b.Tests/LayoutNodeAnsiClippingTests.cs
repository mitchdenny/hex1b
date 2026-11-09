using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Tests;

[TestClass]
public class LayoutNodeAnsiClippingTests
{
    [TestMethod]
    public void ClipString_RightClipsPrintableText_PreservesTrailingResetSuffix()
    {
        var node = new LayoutNode();
        node.Arrange(new Rect(1, 0, 3, 1));

        var text = "\x1b[31mABCDE\x1b[0m";

        var (adjustedX, clipped) = node.ClipString(0, 0, text);

        Assert.AreEqual(1, adjustedX);
        Assert.AreEqual("\x1b[31mBCD\x1b[0m", clipped);
        AssertValidAnsiCsiSequences(clipped);
    }

    [TestMethod]
    public void ClipString_ClipsPlainText_DoesNotIntroduceAnsiCodes()
    {
        var node = new LayoutNode();
        node.Arrange(new Rect(1, 0, 3, 1));

        var text = "ABCDE";

        var (adjustedX, clipped) = node.ClipString(0, 0, text);

        Assert.AreEqual(1, adjustedX);
        Assert.AreEqual("BCD", clipped);
        Assert.DoesNotContain("\x1b[", clipped);
    }

    [TestMethod]
    public void ClipString_WideCharacter_DoesNotSplitEmoji()
    {
        var node = new LayoutNode();
        // Clip region starts at column 0, width 5
        node.Arrange(new Rect(0, 0, 5, 1));

        // "A😀B" = 4 display columns (A=1, 😀=2, B=1)
        var text = "A😀B";

        var (adjustedX, clipped) = node.ClipString(0, 0, text);

        Assert.AreEqual(0, adjustedX);
        Assert.AreEqual("A😀B", clipped); // All fits
    }

    [TestMethod]
    public void ClipString_WideCharacterClippedOnRight_AddsPadding()
    {
        var node = new LayoutNode();
        // Clip region: only 2 columns wide
        node.Arrange(new Rect(0, 0, 2, 1));

        // "A😀" = 3 display columns (A=1, 😀=2), but we only have 2 columns
        var text = "A😀";

        var (adjustedX, clipped) = node.ClipString(0, 0, text);

        Assert.AreEqual(0, adjustedX);
        // Should only include "A" and a space for padding since 😀 doesn't fit
        Assert.AreEqual("A ", clipped);
    }

    [TestMethod]
    public void ClipString_WideCharacterClippedOnLeft_AddsPadding()
    {
        var node = new LayoutNode();
        // Clip region starts at column 1 (middle of emoji)
        node.Arrange(new Rect(1, 0, 3, 1));

        // "😀B" = 3 display columns (😀=2, B=1)
        // Starting at column 1 cuts into the middle of 😀
        var text = "😀B";

        var (adjustedX, clipped) = node.ClipString(0, 0, text);

        Assert.AreEqual(1, adjustedX);
        // Should skip 😀 and add padding, then include B
        Assert.AreEqual(" B", clipped);
    }

    [TestMethod]
    public void ClipString_CJKCharacters_HandledCorrectly()
    {
        var node = new LayoutNode();
        // Clip region: 4 columns wide
        node.Arrange(new Rect(0, 0, 4, 1));

        // "中文" = 4 display columns (each char is 2)
        var text = "中文";

        var (adjustedX, clipped) = node.ClipString(0, 0, text);

        Assert.AreEqual(0, adjustedX);
        Assert.AreEqual("中文", clipped);
    }

    [TestMethod]
    public void ClipString_CJKWithAnsi_PreservesEscapeCodes()
    {
        var node = new LayoutNode();
        node.Arrange(new Rect(0, 0, 4, 1));

        var text = "\x1b[31m中文\x1b[0m"; // 4 display columns

        var (adjustedX, clipped) = node.ClipString(0, 0, text);

        Assert.AreEqual(0, adjustedX);
        Assert.AreEqual("\x1b[31m中文\x1b[0m", clipped);
        AssertValidAnsiCsiSequences(clipped);
    }

    [TestMethod]
    public void ClipString_PlainAsciiThatFits_ReturnsTextAtSameX()
    {
        var node = new LayoutNode();
        node.Arrange(new Rect(0, 0, 80, 1));

        var (adjustedX, clipped) = node.ClipString(2, 0, "Hello World");

        Assert.AreEqual(2, adjustedX);
        Assert.AreEqual("Hello World", clipped);
    }

    [TestMethod]
    public void ClipString_ColouredAsciiThatFits_ReturnsTextUnchanged()
    {
        var node = new LayoutNode();
        node.Arrange(new Rect(0, 0, 80, 1));
        var text = "\x1b[38;2;10;20;30mHello\x1b[0m \x1b[1mWorld\x1b[0m";

        var (adjustedX, clipped) = node.ClipString(0, 0, text);

        Assert.AreEqual(0, adjustedX);
        Assert.AreEqual(text, clipped);
    }

    [TestMethod]
    public void ClipString_WideCharacterSplitAtLeftEdgeWithSgr_PutsPaddingBeforeSgrPrefix()
    {
        var node = new LayoutNode();
        node.Arrange(new Rect(1, 0, 3, 1));

        var (adjustedX, clipped) = node.ClipString(0, 0, "\x1b[31m中文\x1b[0m");

        Assert.AreEqual(1, adjustedX);
        Assert.AreEqual(" \x1b[31m文\x1b[0m", clipped);
    }

    [TestMethod]
    public void ClipString_WideCharacterSplitAtRightEdgeWithSgr_PadsBeforeRestoredReset()
    {
        var node = new LayoutNode();
        node.Arrange(new Rect(0, 0, 2, 1));

        var (adjustedX, clipped) = node.ClipString(0, 0, "\x1b[31mA中B\x1b[0m");

        Assert.AreEqual(0, adjustedX);
        Assert.AreEqual("\x1b[31mA \x1b[0m", clipped);
    }

    [TestMethod]
    public void ClipString_WideCharactersSplitAtBothEdges_PadsBothSides()
    {
        var node = new LayoutNode();
        node.Arrange(new Rect(1, 0, 3, 1));

        var (adjustedX, clipped) = node.ClipString(0, 0, "中AB文");

        Assert.AreEqual(1, adjustedX);
        Assert.AreEqual(" AB ", clipped);
    }

    [TestMethod]
    public void ClipString_HyperlinkClippedOnRight_RestoresClosingSequence()
    {
        var node = new LayoutNode();
        node.Arrange(new Rect(0, 0, 2, 1));
        var closer = "\x1b]8;;\x1b\\";
        var text = "\x1b]8;;http://example.com\x1b\\link" + closer;

        var (adjustedX, clipped) = node.ClipString(0, 0, text);

        Assert.AreEqual(0, adjustedX);
        Assert.AreEqual("\x1b]8;;http://example.com\x1b\\li" + closer, clipped);
    }

    [TestMethod]
    public void ClipString_GraphemeHeavyLine_ClipsOnClusterBoundaries()
    {
        var node = new LayoutNode();
        node.Arrange(new Rect(0, 0, 6, 1));
        var family = "\U0001F468\u200D\U0001F469\u200D\U0001F467";
        var text = $"{family} e\u0301 \U0001F1FA\U0001F1F8 tail";

        var (adjustedX, clipped) = node.ClipString(0, 0, text);

        Assert.AreEqual(0, adjustedX);
        // family (2) + space (1) + e+acute (1) + space (1) = 5; the flag (2) does not fit, so one pad column.
        Assert.AreEqual($"{family} e\u0301  ", clipped);
    }

    [TestMethod]
    public void ClipString_DcsSequence_IsNotRecognisedAndClippedAsText()
    {
        // Pins current behaviour: DCS is not skipped. The leading ESC is dropped as a zero-width
        // grapheme, and the payload counts as visible text, so it is clipped like text.
        var node = new LayoutNode();
        node.Arrange(new Rect(0, 0, 4, 1));

        var (adjustedX, clipped) = node.ClipString(0, 0, "\x1bPq#0\x1b\\AB");

        Assert.AreEqual(0, adjustedX);
        Assert.AreEqual("Pq#0\x1b", clipped);
    }

    [TestMethod]
    public void ClipString_ApcSequence_IsNotRecognisedAndClippedAsText()
    {
        // Pins current behaviour: APC is not skipped. The leading ESC is dropped as a zero-width
        // grapheme, and the payload counts as visible text, so it is clipped like text.
        var node = new LayoutNode();
        node.Arrange(new Rect(0, 0, 4, 1));

        var (adjustedX, clipped) = node.ClipString(0, 0, "\x1b_Gi=1\x1b\\AB");

        Assert.AreEqual(0, adjustedX);
        Assert.AreEqual("_Gi=", clipped);
    }

    [TestMethod]
    public void ClipString_LoneHighSurrogate_IsKeptAsOneColumn()
    {
        var node = new LayoutNode();
        node.Arrange(new Rect(0, 0, 80, 1));

        var (adjustedX, clipped) = node.ClipString(0, 0, "A\uD83DB");

        Assert.AreEqual(0, adjustedX);
        Assert.AreEqual("A\uD83DB", clipped);
    }

    [TestMethod]
    public void ClipString_UnterminatedCsiAfterText_CountsEscapeAndParametersAsVisible()
    {
        // Pins current behaviour: VisibleLength does not treat the incomplete CSI as a sequence
        // (the ESC is zero width, "[31;" counts as 4 columns), while the slicer swallows it.
        var node = new LayoutNode();
        node.Arrange(new Rect(0, 0, 80, 1));

        var (adjustedX, clipped) = node.ClipString(0, 0, "AB\x1b[31;");

        Assert.AreEqual(0, adjustedX);
        Assert.AreEqual("AB\x1b[31;", clipped);
    }

    [TestMethod]
    public void ClipString_UnterminatedOscAfterText_IsKept()
    {
        var node = new LayoutNode();
        node.Arrange(new Rect(0, 0, 80, 1));
        var text = "AB\x1b]8;;http://example.com";

        var (adjustedX, clipped) = node.ClipString(0, 0, text);

        Assert.AreEqual(0, adjustedX);
        Assert.AreEqual(text, clipped);
    }

    [TestMethod]
    public void ClipString_UnterminatedCsiBeforeText_ReturnsEmpty()
    {
        var node = new LayoutNode();
        node.Arrange(new Rect(0, 0, 80, 1));

        var (adjustedX, clipped) = node.ClipString(0, 0, "\x1b[31; 12");

        Assert.AreEqual(0, adjustedX);
        Assert.AreEqual("", clipped);
    }

    [TestMethod]
    public void ClipString_TextEndingInEscape_KeepsTheZeroWidthEscape()
    {
        var node = new LayoutNode();
        node.Arrange(new Rect(0, 0, 80, 1));

        var (adjustedX, clipped) = node.ClipString(0, 0, "AB\x1b");

        Assert.AreEqual(0, adjustedX);
        Assert.AreEqual("AB\x1b", clipped);
    }

    [TestMethod]
    public void ClipString_TextEndingInEscapeClippedOnRight_KeepsOnlyVisibleText()
    {
        var node = new LayoutNode();
        node.Arrange(new Rect(0, 0, 1, 1));

        var (adjustedX, clipped) = node.ClipString(0, 0, "AB\x1b");

        Assert.AreEqual(0, adjustedX);
        Assert.AreEqual("A", clipped);
    }

    [TestMethod]
    public void VisibleLength_PlainAsciiWithSequences_CountsOnlyPrintableColumns()
    {
        Assert.AreEqual(5, AnsiString.VisibleLength("\x1b[31mHello\x1b[0m"));
        Assert.AreEqual(4, AnsiString.VisibleLength("\x1b]8;;http://example.com\x1b\\link\x1b]8;;\x1b\\"));
        Assert.AreEqual(4, AnsiString.VisibleLength("\x1b]8;;http://example.com\x07link\x1b]8;;\x07"));
        Assert.AreEqual(0, AnsiString.VisibleLength("\x1b[31m\x1b[0m"));
        Assert.AreEqual(0, AnsiString.VisibleLength(""));
    }

    [TestMethod]
    public void VisibleLength_UnterminatedSequencesAndStrayEscape_AreMeasuredAsText()
    {
        // Pins current behaviour: only complete CSI and OSC sequences are stripped. Anything else,
        // including the "[31;" after a lone ESC, is measured as text (ESC itself is zero width).
        Assert.AreEqual(2 + 4, AnsiString.VisibleLength("AB\x1b[31;"));
        Assert.AreEqual(2 + 4, AnsiString.VisibleLength("AB\x1b]8;;"));
        Assert.AreEqual(2, AnsiString.VisibleLength("AB\x1b"));
        Assert.AreEqual(3, AnsiString.VisibleLength("A\x1bXB"));
    }

    [TestMethod]
    public void VisibleLength_CombiningMarkAfterRemovedSequence_JoinsThePrecedingCharacter()
    {
        // Pins current behaviour: sequences are stripped before the text is segmented, so a mark
        // that follows a CSI combines with the character before the CSI.
        Assert.AreEqual(1, AnsiString.VisibleLength("e\x1b[31m\u0301"));
        Assert.AreEqual(2, AnsiString.VisibleLength("e\x1b[31m\u0301e"));
    }

    [TestMethod]
    public void VisibleLength_NonAsciiText_MeasuresGraphemeWidths()
    {
        var family = "\U0001F468\u200D\U0001F469\u200D\U0001F467";

        Assert.AreEqual(2 + 1 + 2 + 1 + 2, AnsiString.VisibleLength($"\x1b[31m{family}e\u0301\u4E2D\U0001F1FA\U0001F1F8 \x1b[0m"));
    }

    [TestMethod]
    public void VisibleLength_TextLongerThanScratchBuffer_MeasuresAllColumns()
    {
        var text = "\x1b[31m" + string.Concat(Enumerable.Repeat("\u4E2D", 300)) + "\x1b[0m" + new string('x', 100);

        Assert.AreEqual(300 * 2 + 100, AnsiString.VisibleLength(text));
    }

    private static void AssertValidAnsiCsiSequences(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\x1b')
                continue;

            Assert.IsTrue(i + 1 < text.Length, "Dangling ESC at end");

            if (text[i + 1] != '[')
                continue;

            var foundFinal = false;
            for (var j = i + 2; j < text.Length; j++)
            {
                var c = text[j];
                if (c >= '@' && c <= '~')
                {
                    foundFinal = true;
                    i = j; // skip to end of sequence
                    break;
                }
            }

            Assert.IsTrue(foundFinal, "Incomplete CSI sequence");
        }
    }
}
