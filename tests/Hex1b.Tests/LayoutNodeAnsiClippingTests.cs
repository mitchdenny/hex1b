using Hex1b.Layout;
using Hex1b.Nodes;
using Xunit;

namespace Hex1b.Tests;

public class LayoutNodeAnsiClippingTests
{
    [Fact]
    public void ClipString_RightClipsPrintableText_PreservesTrailingResetSuffix()
    {
        var node = new LayoutNode();
        node.Arrange(new Rect(1, 0, 3, 1));

        var text = "\x1b[31mABCDE\x1b[0m";

        var (adjustedX, clipped) = node.ClipString(0, 0, text);

        Assert.Equal(1, adjustedX);
        Assert.Equal("\x1b[31mBCD\x1b[0m", clipped);
        AssertValidAnsiCsiSequences(clipped);
    }

    [Fact]
    public void ClipString_ClipsPlainText_DoesNotIntroduceAnsiCodes()
    {
        var node = new LayoutNode();
        node.Arrange(new Rect(1, 0, 3, 1));

        var text = "ABCDE";

        var (adjustedX, clipped) = node.ClipString(0, 0, text);

        Assert.Equal(1, adjustedX);
        Assert.Equal("BCD", clipped);
        Assert.DoesNotContain("\x1b[", clipped);
    }

    [Fact]
    public void ClipString_WideCharacter_DoesNotSplitEmoji()
    {
        var node = new LayoutNode();
        // Clip region starts at column 0, width 5
        node.Arrange(new Rect(0, 0, 5, 1));

        // "A😀B" = 4 display columns (A=1, 😀=2, B=1)
        var text = "A😀B";

        var (adjustedX, clipped) = node.ClipString(0, 0, text);

        Assert.Equal(0, adjustedX);
        Assert.Equal("A😀B", clipped); // All fits
    }

    [Fact]
    public void ClipString_WideCharacterClippedOnRight_AddsPadding()
    {
        var node = new LayoutNode();
        // Clip region: only 2 columns wide
        node.Arrange(new Rect(0, 0, 2, 1));

        // "A😀" = 3 display columns (A=1, 😀=2), but we only have 2 columns
        var text = "A😀";

        var (adjustedX, clipped) = node.ClipString(0, 0, text);

        Assert.Equal(0, adjustedX);
        // Should only include "A" and a space for padding since 😀 doesn't fit
        Assert.Equal("A ", clipped);
    }

    [Fact]
    public void ClipString_WideCharacterClippedOnLeft_AddsPadding()
    {
        var node = new LayoutNode();
        // Clip region starts at column 1 (middle of emoji)
        node.Arrange(new Rect(1, 0, 3, 1));

        // "😀B" = 3 display columns (😀=2, B=1)
        // Starting at column 1 cuts into the middle of 😀
        var text = "😀B";

        var (adjustedX, clipped) = node.ClipString(0, 0, text);

        Assert.Equal(1, adjustedX);
        // Should skip 😀 and add padding, then include B
        Assert.Equal(" B", clipped);
    }

    [Fact]
    public void ClipString_CJKCharacters_HandledCorrectly()
    {
        var node = new LayoutNode();
        // Clip region: 4 columns wide
        node.Arrange(new Rect(0, 0, 4, 1));

        // "中文" = 4 display columns (each char is 2)
        var text = "中文";

        var (adjustedX, clipped) = node.ClipString(0, 0, text);

        Assert.Equal(0, adjustedX);
        Assert.Equal("中文", clipped);
    }

    [Fact]
    public void ClipString_CJKWithAnsi_PreservesEscapeCodes()
    {
        var node = new LayoutNode();
        node.Arrange(new Rect(0, 0, 4, 1));

        var text = "\x1b[31m中文\x1b[0m"; // 4 display columns

        var (adjustedX, clipped) = node.ClipString(0, 0, text);

        Assert.Equal(0, adjustedX);
        Assert.Equal("\x1b[31m中文\x1b[0m", clipped);
        AssertValidAnsiCsiSequences(clipped);
    }

    private static void AssertValidAnsiCsiSequences(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\x1b')
                continue;

            Assert.True(i + 1 < text.Length, "Dangling ESC at end");

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

            Assert.True(foundFinal, "Incomplete CSI sequence");
        }
    }
}
