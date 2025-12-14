using Hex1b.Terminal;

namespace Hex1b.Tests;

/// <summary>
/// Tests for DisplayWidth calculations and wide character handling.
/// 
/// Wide characters (CJK, emoji) occupy 2 terminal cells, while
/// combining characters occupy 0 cells.
/// </summary>
public class DisplayWidthTests
{
    #region ASCII Characters
    
    [Fact]
    public void GetStringWidth_AsciiText_EqualsLength()
    {
        Assert.Equal(5, DisplayWidth.GetStringWidth("Hello"));
        Assert.Equal(0, DisplayWidth.GetStringWidth(""));
        Assert.Equal(1, DisplayWidth.GetStringWidth("X"));
    }

    [Fact]
    public void GetStringWidth_AsciiWithSpaces_CountsSpaces()
    {
        Assert.Equal(11, DisplayWidth.GetStringWidth("Hello World"));
        Assert.Equal(3, DisplayWidth.GetStringWidth("   "));
    }

    #endregion

    #region Emoji Width

    [Fact]
    public void GetStringWidth_SimpleEmoji_ReturnsTwoColumns()
    {
        // Simple emoji is 2 cells wide
        Assert.Equal(2, DisplayWidth.GetStringWidth("😀"));
        Assert.Equal(2, DisplayWidth.GetStringWidth("🎉"));
        Assert.Equal(2, DisplayWidth.GetStringWidth("🔥"));
    }

    [Fact]
    public void GetStringWidth_EmojiWithSkinTone_ReturnsTwoColumns()
    {
        // Emoji with skin tone modifier is still 2 cells
        Assert.Equal(2, DisplayWidth.GetStringWidth("👍🏽"));
        Assert.Equal(2, DisplayWidth.GetStringWidth("👋🏻"));
    }

    [Fact]
    public void GetStringWidth_FamilyEmoji_ReturnsTwoColumns()
    {
        // ZWJ family sequence is 2 cells (one visual unit)
        Assert.Equal(2, DisplayWidth.GetStringWidth("👨‍👩‍👧"));
        Assert.Equal(2, DisplayWidth.GetStringWidth("👨‍👩‍👧‍👦"));
    }

    [Fact]
    public void GetStringWidth_FlagEmoji_ReturnsTwoColumns()
    {
        // Flags are 2 cells
        Assert.Equal(2, DisplayWidth.GetStringWidth("🇺🇸"));
        Assert.Equal(2, DisplayWidth.GetStringWidth("🇯🇵"));
    }

    [Fact]
    public void GetStringWidth_MixedTextWithEmoji_CalculatesCorrectly()
    {
        // "Hi" (2) + 😀 (2) + "!" (1) = 5
        Assert.Equal(5, DisplayWidth.GetStringWidth("Hi😀!"));
        
        // "A" (1) + 😀 (2) + 🇺🇸 (2) + "B" (1) = 6
        Assert.Equal(6, DisplayWidth.GetStringWidth("A😀🇺🇸B"));
    }

    #endregion

    #region CJK Characters

    [Fact]
    public void GetStringWidth_CJKCharacters_ReturnsTwoColumnsEach()
    {
        // Chinese characters
        Assert.Equal(2, DisplayWidth.GetStringWidth("中"));
        Assert.Equal(4, DisplayWidth.GetStringWidth("中文"));
        Assert.Equal(6, DisplayWidth.GetStringWidth("你好吗"));
        
        // Japanese hiragana/katakana
        Assert.Equal(2, DisplayWidth.GetStringWidth("あ"));
        Assert.Equal(4, DisplayWidth.GetStringWidth("日本"));
        
        // Korean
        Assert.Equal(2, DisplayWidth.GetStringWidth("한"));
        Assert.Equal(4, DisplayWidth.GetStringWidth("한글"));
    }

    [Fact]
    public void GetStringWidth_MixedCJKAndAscii_CalculatesCorrectly()
    {
        // "Hello" (5) + "中文" (4) = 9
        Assert.Equal(9, DisplayWidth.GetStringWidth("Hello中文"));
        
        // "A" (1) + "日" (2) + "B" (1) + "本" (2) = 6
        Assert.Equal(6, DisplayWidth.GetStringWidth("A日B本"));
    }

    #endregion

    #region Combining Characters

    [Fact]
    public void GetStringWidth_CombiningAccent_CountsAsBaseCharWidth()
    {
        // "e" + combining acute = 1 cell (one visual unit)
        var combiningE = "e\u0301"; // é as e + combining acute
        Assert.Equal(1, DisplayWidth.GetStringWidth(combiningE));
    }

    [Fact]
    public void GetStringWidth_MultipleCombiningMarks_CountsAsBaseCharWidth()
    {
        // "a" + ring above + acute = 1 cell
        var multipleCombining = "a\u030A\u0301";
        Assert.Equal(1, DisplayWidth.GetStringWidth(multipleCombining));
    }

    [Fact]
    public void GetStringWidth_PrecomposedVsCombining_SameWidth()
    {
        var precomposed = "é"; // Single precomposed character
        var combining = "e\u0301"; // e + combining acute
        
        Assert.Equal(1, DisplayWidth.GetStringWidth(precomposed));
        Assert.Equal(1, DisplayWidth.GetStringWidth(combining));
    }

    #endregion

    #region Grapheme Width

    [Fact]
    public void GetGraphemeWidth_SingleAscii_ReturnsOne()
    {
        Assert.Equal(1, DisplayWidth.GetGraphemeWidth("A"));
        Assert.Equal(1, DisplayWidth.GetGraphemeWidth(" "));
    }

    [Fact]
    public void GetGraphemeWidth_Emoji_ReturnsTwo()
    {
        Assert.Equal(2, DisplayWidth.GetGraphemeWidth("😀"));
        Assert.Equal(2, DisplayWidth.GetGraphemeWidth("👨‍👩‍👧"));
    }

    [Fact]
    public void GetGraphemeWidth_CJK_ReturnsTwo()
    {
        Assert.Equal(2, DisplayWidth.GetGraphemeWidth("中"));
        Assert.Equal(2, DisplayWidth.GetGraphemeWidth("あ"));
    }

    [Fact]
    public void GetGraphemeWidth_CombiningSequence_ReturnsBaseWidth()
    {
        Assert.Equal(1, DisplayWidth.GetGraphemeWidth("e\u0301"));
    }

    #endregion

    #region Slice By Display Width

    [Fact]
    public void SliceByDisplayWidth_AsciiText_SlicesCorrectly()
    {
        var (text, columns, _, _) = DisplayWidth.SliceByDisplayWidth("Hello World", 0, 5);
        Assert.Equal("Hello", text);
        Assert.Equal(5, columns);
    }

    [Fact]
    public void SliceByDisplayWidth_WithEmoji_SlicesAtBoundary()
    {
        // "A😀B" - A is 1, 😀 is 2, B is 1
        // Slice 0..3 should give "A😀" (3 columns)
        var (text, columns, _, _) = DisplayWidth.SliceByDisplayWidth("A😀B", 0, 3);
        Assert.Equal("A😀", text);
        Assert.Equal(3, columns);
    }

    [Fact]
    public void SliceByDisplayWidth_CutsBeforeWideChar_WhenNotEnoughSpace()
    {
        // "A😀" - want only 2 columns
        // 😀 needs 2 columns, but we only have 1 left after A
        var (text, columns, _, _) = DisplayWidth.SliceByDisplayWidth("A😀B", 0, 2);
        Assert.Equal("A", text);
        Assert.Equal(1, columns);
    }

    [Fact]
    public void SliceByDisplayWidth_WithCJK_SlicesCorrectly()
    {
        // "中文" is 4 columns (2 + 2)
        var (text, columns, _, _) = DisplayWidth.SliceByDisplayWidth("中文abc", 0, 4);
        Assert.Equal("中文", text);
        Assert.Equal(4, columns);
    }

    [Fact]
    public void SliceByDisplayWidth_FromMiddle_SlicesCorrectly()
    {
        // "Hello" - slice from column 2, length 3 = "llo"
        var (text, columns, _, _) = DisplayWidth.SliceByDisplayWidth("Hello", 2, 3);
        Assert.Equal("llo", text);
        Assert.Equal(3, columns);
    }

    [Fact]
    public void SliceByDisplayWidth_StartInMiddleOfWideChar_SkipsIt()
    {
        // "中文" - start at column 1 (middle of 中), should skip it
        var (text, columns, paddingBefore, _) = DisplayWidth.SliceByDisplayWidth("中文", 1, 3);
        // Should skip 中 and give 文
        Assert.Equal("文", text);
        Assert.Equal(2, columns);
        Assert.Equal(1, paddingBefore); // Need 1 space padding for the cut character
    }

    #endregion

    #region Integration with GraphemeHelper

    [Fact]
    public void GraphemeHelper_GetDisplayWidth_MatchesDisplayWidth()
    {
        var text = "Hello😀世界";
        Assert.Equal(DisplayWidth.GetStringWidth(text), GraphemeHelper.GetDisplayWidth(text));
    }

    [Fact]
    public void GraphemeHelper_IndexToDisplayColumn_CalculatesCorrectly()
    {
        var text = "A😀B";
        // Index 0: before A, column 0
        Assert.Equal(0, GraphemeHelper.IndexToDisplayColumn(text, 0));
        // Index 1: after A, before 😀, column 1
        Assert.Equal(1, GraphemeHelper.IndexToDisplayColumn(text, 1));
        // Index 3: after 😀 (which is 2 chars), before B, column 3
        Assert.Equal(3, GraphemeHelper.IndexToDisplayColumn(text, 3));
        // Index 4: after B, column 4
        Assert.Equal(4, GraphemeHelper.IndexToDisplayColumn(text, 4));
    }

    [Fact]
    public void GraphemeHelper_DisplayColumnToIndex_CalculatesCorrectly()
    {
        var text = "A😀B";
        // Column 0: index 0 (before A)
        Assert.Equal(0, GraphemeHelper.DisplayColumnToIndex(text, 0));
        // Column 1: index 1 (after A)
        Assert.Equal(1, GraphemeHelper.DisplayColumnToIndex(text, 1));
        // Column 2: in middle of 😀, should return start of 😀
        Assert.Equal(1, GraphemeHelper.DisplayColumnToIndex(text, 2));
        // Column 3: after 😀, index 3
        Assert.Equal(3, GraphemeHelper.DisplayColumnToIndex(text, 3));
    }

    #endregion
}
