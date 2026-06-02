
namespace Hex1b.Tests;

/// <summary>
/// Tests for DisplayWidth calculations and wide character handling.
/// 
/// Wide characters (CJK, emoji) occupy 2 terminal cells, while
/// combining characters occupy 0 cells.
/// </summary>
[TestClass]
public class DisplayWidthTests
{
    #region ASCII Characters
    
    [TestMethod]
    public void GetStringWidth_AsciiText_EqualsLength()
    {
        Assert.AreEqual(5, DisplayWidth.GetStringWidth("Hello"));
        Assert.AreEqual(0, DisplayWidth.GetStringWidth(""));
        Assert.AreEqual(1, DisplayWidth.GetStringWidth("X"));
    }

    [TestMethod]
    public void GetStringWidth_AsciiWithSpaces_CountsSpaces()
    {
        Assert.AreEqual(11, DisplayWidth.GetStringWidth("Hello World"));
        Assert.AreEqual(3, DisplayWidth.GetStringWidth("   "));
    }

    #endregion

    #region Emoji Width

    [TestMethod]
    public void GetStringWidth_SimpleEmoji_ReturnsTwoColumns()
    {
        // Simple emoji is 2 cells wide
        Assert.AreEqual(2, DisplayWidth.GetStringWidth("😀"));
        Assert.AreEqual(2, DisplayWidth.GetStringWidth("🎉"));
        Assert.AreEqual(2, DisplayWidth.GetStringWidth("🔥"));
    }

    [TestMethod]
    public void GetStringWidth_EmojiWithSkinTone_ReturnsTwoColumns()
    {
        // Emoji with skin tone modifier is still 2 cells
        Assert.AreEqual(2, DisplayWidth.GetStringWidth("👍🏽"));
        Assert.AreEqual(2, DisplayWidth.GetStringWidth("👋🏻"));
    }

    [TestMethod]
    public void GetStringWidth_FamilyEmoji_ReturnsTwoColumns()
    {
        // ZWJ family sequence is 2 cells (one visual unit)
        Assert.AreEqual(2, DisplayWidth.GetStringWidth("👨‍👩‍👧"));
        Assert.AreEqual(2, DisplayWidth.GetStringWidth("👨‍👩‍👧‍👦"));
    }

    [TestMethod]
    public void GetStringWidth_FlagEmoji_ReturnsTwoColumns()
    {
        // Flags are 2 cells
        Assert.AreEqual(2, DisplayWidth.GetStringWidth("🇺🇸"));
        Assert.AreEqual(2, DisplayWidth.GetStringWidth("🇯🇵"));
    }

    [TestMethod]
    public void GetStringWidth_MixedTextWithEmoji_CalculatesCorrectly()
    {
        // "Hi" (2) + 😀 (2) + "!" (1) = 5
        Assert.AreEqual(5, DisplayWidth.GetStringWidth("Hi😀!"));
        
        // "A" (1) + 😀 (2) + 🇺🇸 (2) + "B" (1) = 6
        Assert.AreEqual(6, DisplayWidth.GetStringWidth("A😀🇺🇸B"));
    }

    #endregion

    #region CJK Characters

    [TestMethod]
    public void GetStringWidth_CJKCharacters_ReturnsTwoColumnsEach()
    {
        // Chinese characters
        Assert.AreEqual(2, DisplayWidth.GetStringWidth("中"));
        Assert.AreEqual(4, DisplayWidth.GetStringWidth("中文"));
        Assert.AreEqual(6, DisplayWidth.GetStringWidth("你好吗"));
        
        // Japanese hiragana/katakana
        Assert.AreEqual(2, DisplayWidth.GetStringWidth("あ"));
        Assert.AreEqual(4, DisplayWidth.GetStringWidth("日本"));
        
        // Korean
        Assert.AreEqual(2, DisplayWidth.GetStringWidth("한"));
        Assert.AreEqual(4, DisplayWidth.GetStringWidth("한글"));
    }

    [TestMethod]
    public void GetStringWidth_MixedCJKAndAscii_CalculatesCorrectly()
    {
        // "Hello" (5) + "中文" (4) = 9
        Assert.AreEqual(9, DisplayWidth.GetStringWidth("Hello中文"));
        
        // "A" (1) + "日" (2) + "B" (1) + "本" (2) = 6
        Assert.AreEqual(6, DisplayWidth.GetStringWidth("A日B本"));
    }

    #endregion

    #region Combining Characters

    [TestMethod]
    public void GetStringWidth_CombiningAccent_CountsAsBaseCharWidth()
    {
        // "e" + combining acute = 1 cell (one visual unit)
        var combiningE = "e\u0301"; // é as e + combining acute
        Assert.AreEqual(1, DisplayWidth.GetStringWidth(combiningE));
    }

    [TestMethod]
    public void GetStringWidth_MultipleCombiningMarks_CountsAsBaseCharWidth()
    {
        // "a" + ring above + acute = 1 cell
        var multipleCombining = "a\u030A\u0301";
        Assert.AreEqual(1, DisplayWidth.GetStringWidth(multipleCombining));
    }

    [TestMethod]
    public void GetStringWidth_PrecomposedVsCombining_SameWidth()
    {
        var precomposed = "é"; // Single precomposed character
        var combining = "e\u0301"; // e + combining acute
        
        Assert.AreEqual(1, DisplayWidth.GetStringWidth(precomposed));
        Assert.AreEqual(1, DisplayWidth.GetStringWidth(combining));
    }

    #endregion

    #region Grapheme Width

    [TestMethod]
    public void GetGraphemeWidth_SingleAscii_ReturnsOne()
    {
        Assert.AreEqual(1, DisplayWidth.GetGraphemeWidth("A"));
        Assert.AreEqual(1, DisplayWidth.GetGraphemeWidth(" "));
    }

    [TestMethod]
    public void GetGraphemeWidth_Emoji_ReturnsTwo()
    {
        Assert.AreEqual(2, DisplayWidth.GetGraphemeWidth("😀"));
        Assert.AreEqual(2, DisplayWidth.GetGraphemeWidth("👨‍👩‍👧"));
    }

    [TestMethod]
    public void GetGraphemeWidth_CJK_ReturnsTwo()
    {
        Assert.AreEqual(2, DisplayWidth.GetGraphemeWidth("中"));
        Assert.AreEqual(2, DisplayWidth.GetGraphemeWidth("あ"));
    }

    [TestMethod]
    public void GetGraphemeWidth_CombiningSequence_ReturnsBaseWidth()
    {
        Assert.AreEqual(1, DisplayWidth.GetGraphemeWidth("e\u0301"));
    }

    #endregion

    #region Known Problematic Characters
    
    /// <summary>
    /// These characters have been observed to cause alignment issues in FullAppDemo.
    /// Each should return width 2 (emoji presentation).
    /// </summary>
    [TestMethod]
    [DataRow("✅", 2)] // U+2705 White Heavy Check Mark
    [DataRow("❌", 2)] // U+274C Cross Mark
    [DataRow("⭐", 2)] // U+2B50 White Medium Star
    [DataRow("⚡", 2)] // U+26A1 High Voltage
    [DataRow("🔴", 2)] // U+1F534 Red Circle
    [DataRow("🟠", 2)] // U+1F7E0 Orange Circle
    [DataRow("🟡", 2)] // U+1F7E1 Yellow Circle
    [DataRow("🟢", 2)] // U+1F7E2 Green Circle
    [DataRow("🔵", 2)] // U+1F535 Blue Circle
    [DataRow("⚫", 2)] // U+26AB Black Circle
    [DataRow("⚪", 2)] // U+26AA White Circle
    [DataRow("⚠️", 2)] // U+26A0+FE0F Warning with VS16
    [DataRow("ℹ️", 2)] // U+2139+FE0F Info with VS16
    [DataRow("❓", 2)] // U+2753 Question Mark Ornament
    [DataRow("❗", 2)] // U+2757 Exclamation Mark
    public void GetGraphemeWidth_ProblematicEmoji_ReturnsTwo(string grapheme, int expectedWidth)
    {
        var actualWidth = DisplayWidth.GetGraphemeWidth(grapheme);
        Assert.AreEqual(expectedWidth, actualWidth);
    }
    
    [TestMethod]
    [DataRow("🖥️", 2)] // U+1F5A5+FE0F Desktop Computer with VS16
    [DataRow("➡️", 2)] // U+27A1+FE0F Right Arrow with VS16
    [DataRow("⬆️", 2)] // U+2B06+FE0F Up Arrow with VS16
    [DataRow("⬇️", 2)] // U+2B07+FE0F Down Arrow with VS16
    [DataRow("⬅️", 2)] // U+2B05+FE0F Left Arrow with VS16
    public void GetGraphemeWidth_VariationSelectorEmoji_ReturnsTwo(string grapheme, int expectedWidth)
    {
        var actualWidth = DisplayWidth.GetGraphemeWidth(grapheme);
        Assert.AreEqual(expectedWidth, actualWidth);
    }

    #endregion

    #region Slice By Display Width

    [TestMethod]
    public void SliceByDisplayWidth_AsciiText_SlicesCorrectly()
    {
        var (text, columns, _, _) = DisplayWidth.SliceByDisplayWidth("Hello World", 0, 5);
        Assert.AreEqual("Hello", text);
        Assert.AreEqual(5, columns);
    }

    [TestMethod]
    public void SliceByDisplayWidth_WithEmoji_SlicesAtBoundary()
    {
        // "A😀B" - A is 1, 😀 is 2, B is 1
        // Slice 0..3 should give "A😀" (3 columns)
        var (text, columns, _, _) = DisplayWidth.SliceByDisplayWidth("A😀B", 0, 3);
        Assert.AreEqual("A😀", text);
        Assert.AreEqual(3, columns);
    }

    [TestMethod]
    public void SliceByDisplayWidth_CutsBeforeWideChar_WhenNotEnoughSpace()
    {
        // "A😀" - want only 2 columns
        // 😀 needs 2 columns, but we only have 1 left after A
        var (text, columns, _, _) = DisplayWidth.SliceByDisplayWidth("A😀B", 0, 2);
        Assert.AreEqual("A", text);
        Assert.AreEqual(1, columns);
    }

    [TestMethod]
    public void SliceByDisplayWidth_WithCJK_SlicesCorrectly()
    {
        // "中文" is 4 columns (2 + 2)
        var (text, columns, _, _) = DisplayWidth.SliceByDisplayWidth("中文abc", 0, 4);
        Assert.AreEqual("中文", text);
        Assert.AreEqual(4, columns);
    }

    [TestMethod]
    public void SliceByDisplayWidth_FromMiddle_SlicesCorrectly()
    {
        // "Hello" - slice from column 2, length 3 = "llo"
        var (text, columns, _, _) = DisplayWidth.SliceByDisplayWidth("Hello", 2, 3);
        Assert.AreEqual("llo", text);
        Assert.AreEqual(3, columns);
    }

    [TestMethod]
    public void SliceByDisplayWidth_StartInMiddleOfWideChar_SkipsIt()
    {
        // "中文" - start at column 1 (middle of 中), should skip it
        var (text, columns, paddingBefore, _) = DisplayWidth.SliceByDisplayWidth("中文", 1, 3);
        // Should skip 中 and give 文
        Assert.AreEqual("文", text);
        Assert.AreEqual(2, columns);
        Assert.AreEqual(1, paddingBefore); // Need 1 space padding for the cut character
    }

    #endregion

    #region Integration with GraphemeHelper

    [TestMethod]
    public void GraphemeHelper_GetDisplayWidth_MatchesDisplayWidth()
    {
        var text = "Hello😀世界";
        Assert.AreEqual(DisplayWidth.GetStringWidth(text), GraphemeHelper.GetDisplayWidth(text));
    }

    [TestMethod]
    public void GraphemeHelper_IndexToDisplayColumn_CalculatesCorrectly()
    {
        var text = "A😀B";
        // Index 0: before A, column 0
        Assert.AreEqual(0, GraphemeHelper.IndexToDisplayColumn(text, 0));
        // Index 1: after A, before 😀, column 1
        Assert.AreEqual(1, GraphemeHelper.IndexToDisplayColumn(text, 1));
        // Index 3: after 😀 (which is 2 chars), before B, column 3
        Assert.AreEqual(3, GraphemeHelper.IndexToDisplayColumn(text, 3));
        // Index 4: after B, column 4
        Assert.AreEqual(4, GraphemeHelper.IndexToDisplayColumn(text, 4));
    }

    [TestMethod]
    public void GraphemeHelper_DisplayColumnToIndex_CalculatesCorrectly()
    {
        var text = "A😀B";
        // Column 0: index 0 (before A)
        Assert.AreEqual(0, GraphemeHelper.DisplayColumnToIndex(text, 0));
        // Column 1: index 1 (after A)
        Assert.AreEqual(1, GraphemeHelper.DisplayColumnToIndex(text, 1));
        // Column 2: in middle of 😀, should return start of 😀
        Assert.AreEqual(1, GraphemeHelper.DisplayColumnToIndex(text, 2));
        // Column 3: after 😀, index 3
        Assert.AreEqual(3, GraphemeHelper.DisplayColumnToIndex(text, 3));
    }

    #endregion
    
    [TestMethod]
    public void GetStringWidth_VariationSelectorEmoji_CalculatesCorrectly()
    {
        // "Test ⚠️ char" = Test(4) + space(1) + ⚠️(2) + space(1) + char(4) = 12
        var text = "Test ⚠️ char";
        var expected = 12;
        var actual = DisplayWidth.GetStringWidth(text);
        
        Assert.AreEqual(expected, actual);
    }
    
    [TestMethod]
    public void GetGraphemeWidth_WarningEmojiWithVS16_ReturnsTwo()
    {
        // ⚠️ is U+26A0 + U+FE0F (warning + variation selector-16)
        var warning = "⚠️";
        
        // Check it's actually the 2-codepoint version
        var runes = warning.EnumerateRunes().ToArray();
        Assert.AreEqual(2, runes.Length);
        Assert.AreEqual(0x26A0, runes[0].Value);  // Warning sign
        Assert.AreEqual(0xFE0F, runes[1].Value);  // Variation selector-16
        
        var width = DisplayWidth.GetGraphemeWidth(warning);
        Assert.AreEqual(2, width);
    }

    [TestMethod]
    public void StringInterpolation_PreservesVariationSelector()
    {
        var emoji = "🖥️";
        var interpolated = $"Test {emoji} char";
        
        // Check that the variation selector is preserved
        var runes = interpolated.EnumerateRunes().ToArray();
        
        // Should contain: T,e,s,t, ,🖥,FE0F, ,c,h,a,r
        var hasVS16 = runes.Any(r => r.Value == 0xFE0F);
        Assert.IsTrue(hasVS16, "Variation selector FE0F should be preserved in interpolated string");
        
        // Check width calculation
        var width = DisplayWidth.GetStringWidth(interpolated);
        // "Test " = 5, 🖥️ = 2, " char" = 5 → total = 12
        Assert.AreEqual(12, width);
    }
    
    [TestMethod]
    public void SliceByDisplayWidthWithAnsi_VS16Emoji_NoPaddingWhenNotClipped()
    {
        // When slicing "Test 🖥️ char" (12 columns) with 28 columns max,
        // there should be no padding since the text fits entirely
        var text = "Test 🖥️ char";
        var (sliced, columns, paddingBefore, paddingAfter) = 
            DisplayWidth.SliceByDisplayWidthWithAnsi(text, 0, 28);
        
        Assert.AreEqual(text, sliced);
        Assert.AreEqual(12, columns);
        Assert.AreEqual(0, paddingBefore);
        Assert.AreEqual(0, paddingAfter);
    }
    
    [TestMethod]
    public void SliceByDisplayWidthWithAnsi_InnerFillSpaces_NoPadding()
    {
        // When slicing 28 spaces with 60 columns max, there should be no padding
        var innerFill = new string(' ', 28);
        var (sliced, columns, paddingBefore, paddingAfter) = 
            DisplayWidth.SliceByDisplayWidthWithAnsi(innerFill, 0, 28);
        
        Assert.AreEqual(28, sliced.Length);
        Assert.AreEqual(28, columns);
        Assert.AreEqual(0, paddingBefore);
        Assert.AreEqual(0, paddingAfter);
    }
    
    #region Checkbox and Symbol Characters
    
    [TestMethod]
    [DataRow("✓", 1)]  // Check Mark U+2713 - NO Emoji_Presentation, defaults to text
    [DataRow("✔", 1)]  // Heavy Check Mark U+2714 - Emoji=Yes but Emoji_Presentation=No (needs VS16 for wide)
    [DataRow("○", 1)]  // White Circle U+25CB - NO Emoji_Presentation
    [DataRow("●", 1)]  // Black Circle U+25CF - NO Emoji_Presentation
    [DataRow("☑", 1)]  // Ballot Box with Check U+2611 - Emoji=Yes but Emoji_Presentation=No (needs VS16 for wide)
    [DataRow("☐", 1)]  // Ballot Box U+2610 - NO Emoji_Presentation
    public void GetGraphemeWidth_CheckboxSymbols_ReturnsExpected(string symbol, int expectedWidth)
    {
        var actualWidth = DisplayWidth.GetGraphemeWidth(symbol);
        Assert.AreEqual(expectedWidth, actualWidth);
    }
    
    [TestMethod]
    public void GetStringWidth_CheckmarkLine_CalculatesCorrectly()
    {
        // "  ✓ Completed Tasks" = 2 spaces + ✓ (1) + space (1) + "Completed Tasks" (15) = 19
        // Note: Using ✓ (U+2713) which defaults to text presentation (width 1)
        var line = "  ✓ Completed Tasks";
        var width = DisplayWidth.GetStringWidth(line);
        Assert.AreEqual(19, width);
    }
    
    [TestMethod]
    public void GetStringWidth_ClipboardEmoji_CalculatesCorrectly()
    {
        // 📋 is a wide emoji (2 columns)
        var clipboard = "📋";
        Assert.AreEqual(2, DisplayWidth.GetGraphemeWidth(clipboard));
        
        // "  📋 Pending Tasks" = 2 spaces + 📋 (2) + space (1) + "Pending Tasks" (13) = 18
        var line = "  📋 Pending Tasks";
        Assert.AreEqual(18, DisplayWidth.GetStringWidth(line));
    }
    
    #endregion
}
