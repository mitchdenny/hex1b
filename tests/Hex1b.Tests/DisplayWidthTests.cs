
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

    #region Known Problematic Characters
    
    /// <summary>
    /// These characters have been observed to cause alignment issues in FullAppDemo.
    /// Each should return width 2 (emoji presentation).
    /// </summary>
    [Theory]
    [InlineData("✅", 2)] // U+2705 White Heavy Check Mark
    [InlineData("❌", 2)] // U+274C Cross Mark
    [InlineData("⭐", 2)] // U+2B50 White Medium Star
    [InlineData("⚡", 2)] // U+26A1 High Voltage
    [InlineData("🔴", 2)] // U+1F534 Red Circle
    [InlineData("🟠", 2)] // U+1F7E0 Orange Circle
    [InlineData("🟡", 2)] // U+1F7E1 Yellow Circle
    [InlineData("🟢", 2)] // U+1F7E2 Green Circle
    [InlineData("🔵", 2)] // U+1F535 Blue Circle
    [InlineData("⚫", 2)] // U+26AB Black Circle
    [InlineData("⚪", 2)] // U+26AA White Circle
    [InlineData("⚠️", 2)] // U+26A0+FE0F Warning with VS16
    [InlineData("ℹ️", 2)] // U+2139+FE0F Info with VS16
    [InlineData("❓", 2)] // U+2753 Question Mark Ornament
    [InlineData("❗", 2)] // U+2757 Exclamation Mark
    public void GetGraphemeWidth_ProblematicEmoji_ReturnsTwo(string grapheme, int expectedWidth)
    {
        var actualWidth = DisplayWidth.GetGraphemeWidth(grapheme);
        Assert.Equal(expectedWidth, actualWidth);
    }
    
    [Theory]
    [InlineData("🖥️", 2)] // U+1F5A5+FE0F Desktop Computer with VS16
    [InlineData("➡️", 2)] // U+27A1+FE0F Right Arrow with VS16
    [InlineData("⬆️", 2)] // U+2B06+FE0F Up Arrow with VS16
    [InlineData("⬇️", 2)] // U+2B07+FE0F Down Arrow with VS16
    [InlineData("⬅️", 2)] // U+2B05+FE0F Left Arrow with VS16
    public void GetGraphemeWidth_VariationSelectorEmoji_ReturnsTwo(string grapheme, int expectedWidth)
    {
        var actualWidth = DisplayWidth.GetGraphemeWidth(grapheme);
        Assert.Equal(expectedWidth, actualWidth);
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
    
    [Fact]
    public void GetStringWidth_VariationSelectorEmoji_CalculatesCorrectly()
    {
        // "Test ⚠️ char" = Test(4) + space(1) + ⚠️(2) + space(1) + char(4) = 12
        var text = "Test ⚠️ char";
        var expected = 12;
        var actual = DisplayWidth.GetStringWidth(text);
        
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    public void GetGraphemeWidth_WarningEmojiWithVS16_ReturnsTwo()
    {
        // ⚠️ is U+26A0 + U+FE0F (warning + variation selector-16)
        var warning = "⚠️";
        
        // Check it's actually the 2-codepoint version
        var runes = warning.EnumerateRunes().ToArray();
        Assert.Equal(2, runes.Length);
        Assert.Equal(0x26A0, runes[0].Value);  // Warning sign
        Assert.Equal(0xFE0F, runes[1].Value);  // Variation selector-16
        
        var width = DisplayWidth.GetGraphemeWidth(warning);
        Assert.Equal(2, width);
    }

    [Fact]
    public void StringInterpolation_PreservesVariationSelector()
    {
        var emoji = "🖥️";
        var interpolated = $"Test {emoji} char";
        
        // Check that the variation selector is preserved
        var runes = interpolated.EnumerateRunes().ToArray();
        
        // Should contain: T,e,s,t, ,🖥,FE0F, ,c,h,a,r
        var hasVS16 = runes.Any(r => r.Value == 0xFE0F);
        Assert.True(hasVS16, "Variation selector FE0F should be preserved in interpolated string");
        
        // Check width calculation
        var width = DisplayWidth.GetStringWidth(interpolated);
        // "Test " = 5, 🖥️ = 2, " char" = 5 → total = 12
        Assert.Equal(12, width);
    }
    
    [Fact]
    public void SliceByDisplayWidthWithAnsi_VS16Emoji_NoPaddingWhenNotClipped()
    {
        // When slicing "Test 🖥️ char" (12 columns) with 28 columns max,
        // there should be no padding since the text fits entirely
        var text = "Test 🖥️ char";
        var (sliced, columns, paddingBefore, paddingAfter) = 
            DisplayWidth.SliceByDisplayWidthWithAnsi(text, 0, 28);
        
        Assert.Equal(text, sliced);
        Assert.Equal(12, columns);
        Assert.Equal(0, paddingBefore);
        Assert.Equal(0, paddingAfter);
    }
    
    [Fact]
    public void SliceByDisplayWidthWithAnsi_InnerFillSpaces_NoPadding()
    {
        // When slicing 28 spaces with 60 columns max, there should be no padding
        var innerFill = new string(' ', 28);
        var (sliced, columns, paddingBefore, paddingAfter) = 
            DisplayWidth.SliceByDisplayWidthWithAnsi(innerFill, 0, 28);
        
        Assert.Equal(28, sliced.Length);
        Assert.Equal(28, columns);
        Assert.Equal(0, paddingBefore);
        Assert.Equal(0, paddingAfter);
    }
    
    #region Checkbox and Symbol Characters
    
    [Theory]
    [InlineData("✓", 1)]  // Check Mark U+2713 - NO Emoji_Presentation, defaults to text
    [InlineData("✔", 1)]  // Heavy Check Mark U+2714 - Emoji=Yes but Emoji_Presentation=No (needs VS16 for wide)
    [InlineData("○", 1)]  // White Circle U+25CB - NO Emoji_Presentation
    [InlineData("●", 1)]  // Black Circle U+25CF - NO Emoji_Presentation
    [InlineData("☑", 1)]  // Ballot Box with Check U+2611 - Emoji=Yes but Emoji_Presentation=No (needs VS16 for wide)
    [InlineData("☐", 1)]  // Ballot Box U+2610 - NO Emoji_Presentation
    public void GetGraphemeWidth_CheckboxSymbols_ReturnsExpected(string symbol, int expectedWidth)
    {
        var actualWidth = DisplayWidth.GetGraphemeWidth(symbol);
        Assert.Equal(expectedWidth, actualWidth);
    }
    
    [Fact]
    public void GetStringWidth_CheckmarkLine_CalculatesCorrectly()
    {
        // "  ✓ Completed Tasks" = 2 spaces + ✓ (1) + space (1) + "Completed Tasks" (15) = 19
        // Note: Using ✓ (U+2713) which defaults to text presentation (width 1)
        var line = "  ✓ Completed Tasks";
        var width = DisplayWidth.GetStringWidth(line);
        Assert.Equal(19, width);
    }
    
    [Fact]
    public void GetStringWidth_ClipboardEmoji_CalculatesCorrectly()
    {
        // 📋 is a wide emoji (2 columns)
        var clipboard = "📋";
        Assert.Equal(2, DisplayWidth.GetGraphemeWidth(clipboard));
        
        // "  📋 Pending Tasks" = 2 spaces + 📋 (2) + space (1) + "Pending Tasks" (13) = 18
        var line = "  📋 Pending Tasks";
        Assert.Equal(18, DisplayWidth.GetStringWidth(line));
    }
    
    #endregion
}
