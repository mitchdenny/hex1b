// Fitzpatrick skin tone modifier conformance tests
//
// These tests verify that Hex1b handles Fitzpatrick skin tone modifiers (U+1F3FB–1F3FF)
// correctly in a terminal emulator context. The key issue is that .NET's
// StringInfo.GetTextElementEnumerator follows Unicode TR29 grapheme clustering, which
// gives Fitzpatrick modifiers Grapheme_Cluster_Break=Extend — causing them to combine
// with ANY preceding character. Terminal emulators must only combine them when the
// preceding character is a valid Emoji_Modifier_Base (hands, faces, people, etc.).
//
// Without this special handling:
//   '"' + U+1F3FF → .NET groups as 1 grapheme (width 1), cursor at 1
// With correct terminal handling:
//   '"' + U+1F3FF → 2 separate characters: '"' (1 cell) + '🏿' (2 cells), cursor at 3
//
// This matches the behavior of Ghostty, kitty, WezTerm, and other conformant terminals.

namespace Hex1b.Tests.Conformance.Ghostty;

[TestCategory("GhosttyConformance")]
[TestClass]
public class GhosttyFitzpatrickConformanceTests
{
    // =================================================================
    // Core Ghostty-translated tests
    // =================================================================

    // Ghostty: "Terminal: Fitzpatrick skin tone next valid base"
    // 👋 (U+1F44B) is Emoji_Modifier_Base — modifier should combine with it.
    [TestMethod]
    public void FitzpatrickSkinTone_ValidBase_CombinesIntoSingleGrapheme()
    {
        var terminal = GhosttyTestFixture.CreateTerminal(80, 80);
        
        // 👋🏿 (waving hand + dark skin tone) — valid combination
        GhosttyTestFixture.Feed(terminal, "\U0001F44B\U0001F3FF");
        
        // Should combine into one grapheme taking 2 cells
        Assert.AreEqual(0, terminal.CursorY);
        Assert.AreEqual(2, terminal.CursorX);
        
        // Cell 0 should contain the waving hand (base emoji)
        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.AreEqual("👋🏿", cell0.Character);
    }

    // Ghostty: "Terminal: Fitzpatrick skin tone next to non-base"
    // '"' (U+0022) is NOT Emoji_Modifier_Base — modifier must be standalone.
    [TestMethod]
    public void FitzpatrickSkinTone_NonBase_Quote_SplitsIntoSeparateCharacters()
    {
        var terminal = GhosttyTestFixture.CreateTerminal(80, 80);
        
        // " + 🏿 + " — skin tone should NOT combine with quote
        GhosttyTestFixture.Feed(terminal, "\"\U0001F3FF\"");
        
        // " (1 cell) + 🏿 (2 cells) + " (1 cell) = 4 cells
        Assert.AreEqual(0, terminal.CursorY);
        Assert.AreEqual(4, terminal.CursorX);
        
        // Cell 0: quote mark
        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.AreEqual("\"", cell0.Character);
        
        // Cell 1: dark skin tone (wide)
        var cell1 = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.AreEqual("\U0001F3FF", cell1.Character);
        
        // Cell 3: closing quote (cell 2 is the wide char continuation)
        var cell3 = GhosttyTestFixture.GetCell(terminal, 0, 3);
        Assert.AreEqual("\"", cell3.Character);
    }

    // =================================================================
    // All five Fitzpatrick modifiers with non-base
    // =================================================================

    [TestMethod]
    [DataRow(0x1F3FB, "Light")]
    [DataRow(0x1F3FC, "MediumLight")]
    [DataRow(0x1F3FD, "Medium")]
    [DataRow(0x1F3FE, "MediumDark")]
    [DataRow(0x1F3FF, "Dark")]
    public void FitzpatrickSkinTone_AllModifiers_NonBase_SplitCorrectly(int modifierCodepoint, string _)
    {
        var terminal = GhosttyTestFixture.CreateTerminal(80, 80);
        var modifier = char.ConvertFromUtf32(modifierCodepoint);
        
        // 'A' is not Emoji_Modifier_Base — modifier should be standalone
        GhosttyTestFixture.Feed(terminal, "A" + modifier);
        
        // A (1 cell) + modifier (2 cells) = 3 cells
        Assert.AreEqual(3, terminal.CursorX);
        
        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.AreEqual("A", cell0.Character);
        
        var cell1 = GhosttyTestFixture.GetCell(terminal, 0, 1);
        Assert.AreEqual(modifier, cell1.Character);
    }

    // =================================================================
    // All five Fitzpatrick modifiers with valid base
    // =================================================================

    [TestMethod]
    [DataRow(0x1F3FB)]
    [DataRow(0x1F3FC)]
    [DataRow(0x1F3FD)]
    [DataRow(0x1F3FE)]
    [DataRow(0x1F3FF)]
    public void FitzpatrickSkinTone_AllModifiers_ValidBase_CombineCorrectly(int modifierCodepoint)
    {
        var terminal = GhosttyTestFixture.CreateTerminal(80, 80);
        var modifier = char.ConvertFromUtf32(modifierCodepoint);
        
        // 👍 (U+1F44D) is Emoji_Modifier_Base — modifier should combine
        GhosttyTestFixture.Feed(terminal, "\U0001F44D" + modifier);
        
        // Combined grapheme takes 2 cells
        Assert.AreEqual(2, terminal.CursorX);
        
        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.AreEqual("\U0001F44D" + modifier, cell0.Character);
    }

    // =================================================================
    // Various non-base character types
    // =================================================================

    [TestMethod]
    public void FitzpatrickSkinTone_NonBase_AsciiLetter()
    {
        var terminal = GhosttyTestFixture.CreateTerminal(80, 80);
        
        // 'n' + skin tone — must not combine
        GhosttyTestFixture.Feed(terminal, "n\U0001F3FF");
        
        Assert.AreEqual(3, terminal.CursorX); // n(1) + 🏿(2)
        Assert.AreEqual("n", GhosttyTestFixture.GetCell(terminal, 0, 0).Character);
        Assert.AreEqual("\U0001F3FF", GhosttyTestFixture.GetCell(terminal, 0, 1).Character);
    }

    [TestMethod]
    public void FitzpatrickSkinTone_NonBase_Space()
    {
        var terminal = GhosttyTestFixture.CreateTerminal(80, 80);
        
        // space + skin tone — must not combine
        GhosttyTestFixture.Feed(terminal, " \U0001F3FF");
        
        Assert.AreEqual(3, terminal.CursorX); // space(1) + 🏿(2)
        Assert.AreEqual(" ", GhosttyTestFixture.GetCell(terminal, 0, 0).Character);
        Assert.AreEqual("\U0001F3FF", GhosttyTestFixture.GetCell(terminal, 0, 1).Character);
    }

    [TestMethod]
    public void FitzpatrickSkinTone_NonBase_Digit()
    {
        var terminal = GhosttyTestFixture.CreateTerminal(80, 80);
        
        // '5' + skin tone — must not combine
        GhosttyTestFixture.Feed(terminal, "5\U0001F3FF");
        
        Assert.AreEqual(3, terminal.CursorX);
        Assert.AreEqual("5", GhosttyTestFixture.GetCell(terminal, 0, 0).Character);
        Assert.AreEqual("\U0001F3FF", GhosttyTestFixture.GetCell(terminal, 0, 1).Character);
    }

    [TestMethod]
    public void FitzpatrickSkinTone_NonBase_NonPersonEmoji()
    {
        var terminal = GhosttyTestFixture.CreateTerminal(80, 80);
        
        // 🌍 (U+1F30D, Earth globe) is NOT Emoji_Modifier_Base — must not combine
        GhosttyTestFixture.Feed(terminal, "\U0001F30D\U0001F3FF");
        
        Assert.AreEqual(4, terminal.CursorX); // 🌍(2) + 🏿(2)
        Assert.AreEqual("\U0001F30D", GhosttyTestFixture.GetCell(terminal, 0, 0).Character);
        Assert.AreEqual("\U0001F3FF", GhosttyTestFixture.GetCell(terminal, 0, 2).Character);
    }

    [TestMethod]
    public void FitzpatrickSkinTone_NonBase_Heart()
    {
        var terminal = GhosttyTestFixture.CreateTerminal(80, 80);
        
        // ❤ (U+2764) is NOT Emoji_Modifier_Base — must not combine
        // Note: ❤ is text-presentation by default, so width 1 without VS16
        GhosttyTestFixture.Feed(terminal, "\u2764\U0001F3FF");
        
        Assert.AreEqual(3, terminal.CursorX); // ❤(1) + 🏿(2)
        Assert.AreEqual("\u2764", GhosttyTestFixture.GetCell(terminal, 0, 0).Character);
        Assert.AreEqual("\U0001F3FF", GhosttyTestFixture.GetCell(terminal, 0, 1).Character);
    }

    // =================================================================
    // Emoji_Modifier_Base characters (valid combinations)
    // =================================================================

    [TestMethod]
    public void FitzpatrickSkinTone_ValidBase_IndexFinger()
    {
        var terminal = GhosttyTestFixture.CreateTerminal(80, 80);
        
        // ☝ (U+261D) is Emoji_Modifier_Base (BMP)
        GhosttyTestFixture.Feed(terminal, "\u261D\U0001F3FD");
        
        // Should combine — ☝ is text-presentation by default (1 cell) but with
        // skin tone modifier it forms a combined emoji grapheme
        Assert.AreEqual(0, terminal.CursorY);
        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.AreEqual("\u261D\U0001F3FD", cell0.Character);
    }

    [TestMethod]
    public void FitzpatrickSkinTone_ValidBase_RaisedFist()
    {
        var terminal = GhosttyTestFixture.CreateTerminal(80, 80);
        
        // ✊ (U+270A) is Emoji_Modifier_Base
        GhosttyTestFixture.Feed(terminal, "\u270A\U0001F3FB");
        
        Assert.AreEqual(2, terminal.CursorX);
        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.AreEqual("\u270A\U0001F3FB", cell0.Character);
    }

    [TestMethod]
    public void FitzpatrickSkinTone_ValidBase_FlexedBiceps()
    {
        var terminal = GhosttyTestFixture.CreateTerminal(80, 80);
        
        // 💪 (U+1F4AA) is Emoji_Modifier_Base
        GhosttyTestFixture.Feed(terminal, "\U0001F4AA\U0001F3FE");
        
        Assert.AreEqual(2, terminal.CursorX);
        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.AreEqual("\U0001F4AA\U0001F3FE", cell0.Character);
    }

    [TestMethod]
    public void FitzpatrickSkinTone_ValidBase_PersonBowingDeeply()
    {
        var terminal = GhosttyTestFixture.CreateTerminal(80, 80);
        
        // 🙇 (U+1F647) is Emoji_Modifier_Base
        GhosttyTestFixture.Feed(terminal, "\U0001F647\U0001F3FC");
        
        Assert.AreEqual(2, terminal.CursorX);
        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.AreEqual("\U0001F647\U0001F3FC", cell0.Character);
    }

    // =================================================================
    // Edge cases
    // =================================================================

    [TestMethod]
    public void FitzpatrickSkinTone_AtStartOfLine()
    {
        var terminal = GhosttyTestFixture.CreateTerminal(80, 80);
        
        // Skin tone modifier alone at start of line — should be standalone wide char
        GhosttyTestFixture.Feed(terminal, "\U0001F3FF");
        
        Assert.AreEqual(2, terminal.CursorX);
        var cell0 = GhosttyTestFixture.GetCell(terminal, 0, 0);
        Assert.AreEqual("\U0001F3FF", cell0.Character);
    }

    [TestMethod]
    public void FitzpatrickSkinTone_MultipleInSequence_NonBase()
    {
        var terminal = GhosttyTestFixture.CreateTerminal(80, 80);
        
        // Two skin tone modifiers after non-base: each should be standalone
        GhosttyTestFixture.Feed(terminal, "x\U0001F3FB\U0001F3FF");
        
        // x(1) + 🏻(2) + 🏿(2) = 5 cells
        Assert.AreEqual(5, terminal.CursorX);
        Assert.AreEqual("x", GhosttyTestFixture.GetCell(terminal, 0, 0).Character);
        Assert.AreEqual("\U0001F3FB", GhosttyTestFixture.GetCell(terminal, 0, 1).Character);
        Assert.AreEqual("\U0001F3FF", GhosttyTestFixture.GetCell(terminal, 0, 3).Character);
    }

    [TestMethod]
    public void FitzpatrickSkinTone_ValidBase_ThenNonBase_InSequence()
    {
        var terminal = GhosttyTestFixture.CreateTerminal(80, 80);
        
        // 👋🏿 (valid combo) then x🏻 (invalid combo)
        GhosttyTestFixture.Feed(terminal, "\U0001F44B\U0001F3FF" + "x\U0001F3FB");
        
        // 👋🏿(2) + x(1) + 🏻(2) = 5 cells
        Assert.AreEqual(5, terminal.CursorX);
        Assert.AreEqual("\U0001F44B\U0001F3FF", GhosttyTestFixture.GetCell(terminal, 0, 0).Character);
        Assert.AreEqual("x", GhosttyTestFixture.GetCell(terminal, 0, 2).Character);
        Assert.AreEqual("\U0001F3FB", GhosttyTestFixture.GetCell(terminal, 0, 3).Character);
    }

    [TestMethod]
    public void FitzpatrickSkinTone_NonBase_CJK()
    {
        var terminal = GhosttyTestFixture.CreateTerminal(80, 80);
        
        // CJK character (漢) is NOT Emoji_Modifier_Base — must split
        GhosttyTestFixture.Feed(terminal, "\u6F22\U0001F3FF");
        
        Assert.AreEqual(4, terminal.CursorX); // 漢(2) + 🏿(2)
        Assert.AreEqual("\u6F22", GhosttyTestFixture.GetCell(terminal, 0, 0).Character);
        Assert.AreEqual("\U0001F3FF", GhosttyTestFixture.GetCell(terminal, 0, 2).Character);
    }

    [TestMethod]
    public void FitzpatrickSkinTone_NonBase_Punctuation()
    {
        var terminal = GhosttyTestFixture.CreateTerminal(80, 80);
        
        // Various punctuation — none are Emoji_Modifier_Base
        GhosttyTestFixture.Feed(terminal, "!\U0001F3FF");
        
        Assert.AreEqual(3, terminal.CursorX); // !(1) + 🏿(2)
        Assert.AreEqual("!", GhosttyTestFixture.GetCell(terminal, 0, 0).Character);
        Assert.AreEqual("\U0001F3FF", GhosttyTestFixture.GetCell(terminal, 0, 1).Character);
    }

    // =================================================================
    // DisplayWidth.IsEmojiModifierBase unit tests
    // =================================================================

    [TestMethod]
    // BMP Emoji_Modifier_Base
    [DataRow(0x261D, true)]   // ☝ Index pointing up
    [DataRow(0x26F9, true)]   // ⛹ Person bouncing ball
    [DataRow(0x270A, true)]   // ✊ Raised fist
    [DataRow(0x270B, true)]   // ✋ Raised hand
    [DataRow(0x270C, true)]   // ✌ Victory hand
    [DataRow(0x270D, true)]   // ✍ Writing hand
    // SMP Emoji_Modifier_Base  
    [DataRow(0x1F385, true)]  // 🎅 Santa Claus
    [DataRow(0x1F3C2, true)]  // 🏂 Snowboarder
    [DataRow(0x1F44B, true)]  // 👋 Waving hand
    [DataRow(0x1F44D, true)]  // 👍 Thumbs up
    [DataRow(0x1F4AA, true)]  // 💪 Flexed biceps
    [DataRow(0x1F590, true)]  // 🖐 Raised hand with fingers splayed
    [DataRow(0x1F64F, true)]  // 🙏 Folded hands
    [DataRow(0x1F926, true)]  // 🤦 Facepalm
    [DataRow(0x1F9D1, true)]  // 🧑 Person
    [DataRow(0x1FAF0, true)]  // 🫰 Hand with index finger and thumb crossed
    [DataRow(0x1FAF8, true)]  // 🫸 Rightwards pushing hand
    // NOT Emoji_Modifier_Base
    [DataRow(0x0022, false)]  // " Quote
    [DataRow(0x0041, false)]  // A
    [DataRow(0x0020, false)]  // Space
    [DataRow(0x2764, false)]  // ❤ Heart (not modifier base)
    [DataRow(0x1F30D, false)] // 🌍 Earth globe
    [DataRow(0x1F600, false)] // 😀 Grinning face
    [DataRow(0x1F4A9, false)] // 💩 Pile of poo
    [DataRow(0x1F3FB, false)] // Fitzpatrick modifier itself is not a base
    public void IsEmojiModifierBase_ReturnsCorrectResult(int codePoint, bool expected)
    {
        Assert.AreEqual(expected, DisplayWidth.IsEmojiModifierBase(codePoint));
    }

    [TestMethod]
    [DataRow(0x1F3FB, true)]  // Light skin tone
    [DataRow(0x1F3FC, true)]  // Medium-light skin tone
    [DataRow(0x1F3FD, true)]  // Medium skin tone
    [DataRow(0x1F3FE, true)]  // Medium-dark skin tone
    [DataRow(0x1F3FF, true)]  // Dark skin tone
    [DataRow(0x1F3FA, false)] // Before range (Amphora 🏺)
    [DataRow(0x1F400, false)] // After range (Rat 🐀)
    [DataRow(0x0041, false)]  // ASCII A
    public void IsFitzpatrickModifier_ReturnsCorrectResult(int codePoint, bool expected)
    {
        Assert.AreEqual(expected, DisplayWidth.IsFitzpatrickModifier(codePoint));
    }
}
