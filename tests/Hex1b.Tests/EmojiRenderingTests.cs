using Hex1b.Terminal;
using Hex1b.Terminal.Automation;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests that verify emoji rendering in the terminal and capture SVG/HTML for visual inspection.
/// These tests exercise the terminal's ability to handle:
/// - Surrogate pairs (basic emoji)
/// - Skin tone modifiers
/// - ZWJ (Zero-Width Joiner) sequences
/// - Flag emoji (regional indicators)
/// - Emoji with variation selectors
/// - Wide character display widths
/// </summary>
public class EmojiRenderingTests
{
    /// <summary>
    /// Simple synchronous test terminal.
    /// </summary>
    private sealed class TestTerminal : IDisposable
    {
        private readonly StreamWorkloadAdapter _workload;

        public Hex1bTerminal Terminal { get; }

        public TestTerminal(int width, int height)
        {
            _workload = StreamWorkloadAdapter.CreateHeadless(width, height);
            Terminal = new Hex1bTerminal(_workload, width, height);
        }

        public void Write(string text)
        {
            Terminal.ApplyTokens(AnsiTokenizer.Tokenize(text));
        }

        public void Dispose()
        {
            Terminal.Dispose();
            _workload.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    #region Emoji Test Data

    // Simple emoji (surrogate pairs)
    private const string Smile = "😀";
    private const string Heart = "❤️";
    private const string Star = "⭐";
    private const string Fire = "🔥";
    private const string Rocket = "🚀";
    private const string Check = "✅";
    private const string X = "❌";
    private const string Warning = "⚠️";
    
    // Emoji with skin tone modifiers
    private const string ThumbsUp = "👍";
    private const string ThumbsUpLight = "👍🏻";
    private const string ThumbsUpMediumLight = "👍🏼";
    private const string ThumbsUpMedium = "👍🏽";
    private const string ThumbsUpMediumDark = "👍🏾";
    private const string ThumbsUpDark = "👍🏿";
    
    // ZWJ (Zero-Width Joiner) sequences - family emoji
    private const string Family = "👨‍👩‍👧";
    private const string FamilyTwo = "👨‍👩‍👧‍👦";
    private const string Couple = "👩‍❤️‍👨";
    
    // ZWJ sequences - profession emoji
    private const string ManTechnologist = "👨‍💻";
    private const string WomanScientist = "👩‍🔬";
    private const string WomanAstronaut = "👩‍🚀";
    
    // Flag emoji (regional indicators)
    private const string FlagUS = "🇺🇸";
    private const string FlagUK = "🇬🇧";
    private const string FlagJP = "🇯🇵";
    private const string FlagDE = "🇩🇪";
    private const string FlagFR = "🇫🇷";
    private const string FlagAU = "🇦🇺";
    
    // Keycap sequences
    private const string Keycap0 = "0️⃣";
    private const string Keycap1 = "1️⃣";
    private const string Keycap2 = "2️⃣";
    private const string KeycapHash = "#️⃣";
    private const string KeycapStar = "*️⃣";
    
    // Symbols and misc
    private const string Rainbow = "🌈";
    private const string Unicorn = "🦄";
    private const string Pizza = "🍕";
    private const string Beer = "🍺";
    private const string PartyPopper = "🎉";
    private const string Sparkles = "✨";

    #endregion

    /// <summary>
    /// Renders a variety of simple emoji (single grapheme, surrogate pairs).
    /// </summary>
    [Fact]
    public void SimpleEmoji_RendersCorrectly()
    {
        using var test = new TestTerminal(60, 20);

        test.Write("Simple Emoji Rendering\n");
        test.Write("======================\n\n");

        test.Write($"Faces:    {Smile} 😃 😄 😁 😆 😅 🤣 😂 🙂 🙃\n");
        test.Write($"Hearts:   {Heart} 💛 💚 💙 💜 🖤 🤍 🤎 💔 💕\n");
        test.Write($"Gestures: 👋 🤚 🖐️ ✋ 🖖 👌 🤌 🤏 ✌️ 🤞\n");
        test.Write($"Animals:  🐶 🐱 🐭 🐹 🐰 🦊 🐻 🐼 🐨 🐯\n");
        test.Write($"Food:     {Pizza} 🍔 🍟 🌭 🍿 🧂 🥓 🥚 🍳 🧇\n");
        test.Write($"Objects:  {Rocket} {Fire} {Star} {Check} {X} {Warning} 💡 📱 💻 ⌨️\n");
        test.Write($"Nature:   {Rainbow} 🌞 🌙 ⭐ 🌟 {Sparkles} ☀️ 🌤️ ⛅ 🌧️\n");
        test.Write($"Fun:      {PartyPopper} 🎊 🎈 🎁 🎀 🎗️ 🏆 🥇 🎯 🎮\n");

        var snapshot = test.Terminal.CreateSnapshot();

        // Verify some key emoji are present
        Assert.True(ContainsGrapheme(snapshot, "😀"), "Smile emoji should be present");
        Assert.True(ContainsGrapheme(snapshot, "🚀"), "Rocket emoji should be present");
        Assert.True(ContainsGrapheme(snapshot, "🍕"), "Pizza emoji should be present");
        
        // Debug: Check the cell state for a simple emoji line
        // "Faces:    " is 10 chars, then first emoji at col 10
        var emojiCell = snapshot.GetCell(10, 3); // Row 4 (0-indexed=3), "Faces:" line
        var contCell = snapshot.GetCell(11, 3);
        
        // Verify emoji is in cell and continuation is empty with matching sequence
        Assert.Equal("😀", emojiCell.Character);
        Assert.Equal("", contCell.Character);
        Assert.Equal(emojiCell.Sequence, contCell.Sequence);

        TestCaptureHelper.Capture(test.Terminal, "simple-emoji");
    }

    /// <summary>
    /// Renders emoji with skin tone modifiers.
    /// </summary>
    [Fact]
    public void SkinToneModifiers_RendersCorrectly()
    {
        using var test = new TestTerminal(70, 18);

        test.Write("Emoji Skin Tone Modifiers\n");
        test.Write("=========================\n\n");

        test.Write($"Thumbs Up:  {ThumbsUp} {ThumbsUpLight} {ThumbsUpMediumLight} {ThumbsUpMedium} {ThumbsUpMediumDark} {ThumbsUpDark}\n");
        test.Write($"Wave:       👋 👋🏻 👋🏼 👋🏽 👋🏾 👋🏿\n");
        test.Write($"Clap:       👏 👏🏻 👏🏼 👏🏽 👏🏾 👏🏿\n");
        test.Write($"Point Up:   👆 👆🏻 👆🏼 👆🏽 👆🏾 👆🏿\n");
        test.Write($"Raised:     🙋 🙋🏻 🙋🏼 🙋🏽 🙋🏾 🙋🏿\n");
        test.Write($"Person:     🧑 🧑🏻 🧑🏼 🧑🏽 🧑🏾 🧑🏿\n");
        test.Write($"Woman:      👩 👩🏻 👩🏼 👩🏽 👩🏾 👩🏿\n");
        test.Write($"Man:        👨 👨🏻 👨🏼 👨🏽 👨🏾 👨🏿\n");
        
        test.Write("\nNote: Each row shows default + 5 skin tone variants\n");

        var snapshot = test.Terminal.CreateSnapshot();

        // Verify skin tone variants are present
        Assert.True(ContainsGrapheme(snapshot, ThumbsUpLight), "Light skin tone should be present");
        Assert.True(ContainsGrapheme(snapshot, ThumbsUpDark), "Dark skin tone should be present");

        TestCaptureHelper.Capture(test.Terminal, "skin-tone-modifiers");
    }

    /// <summary>
    /// Renders ZWJ (Zero-Width Joiner) sequences like families and professions.
    /// </summary>
    [Fact]
    public void ZwjSequences_RendersCorrectly()
    {
        using var test = new TestTerminal(70, 20);

        test.Write("ZWJ (Zero-Width Joiner) Sequences\n");
        test.Write("=================================\n\n");

        test.Write("Family Compositions:\n");
        test.Write($"  Family (M+W+G):    {Family}\n");
        test.Write($"  Family (M+W+G+B):  {FamilyTwo}\n");
        test.Write($"  Couple:            {Couple}\n");
        test.Write($"  Women holding:     👩‍❤️‍👩\n");
        test.Write($"  Men holding:       👨‍❤️‍👨\n");

        test.Write("\nProfession Sequences:\n");
        test.Write($"  Man Technologist:  {ManTechnologist}\n");
        test.Write($"  Woman Scientist:   {WomanScientist}\n");
        test.Write($"  Woman Astronaut:   {WomanAstronaut}\n");
        test.Write($"  Man Firefighter:   👨‍🚒\n");
        test.Write($"  Woman Doctor:      👩‍⚕️\n");

        test.Write("\nNote: ZWJ sequences combine multiple emoji into one grapheme\n");

        var snapshot = test.Terminal.CreateSnapshot();

        // Verify ZWJ sequences are present as single graphemes
        Assert.True(ContainsGrapheme(snapshot, Family), "Family emoji should be present");
        Assert.True(ContainsGrapheme(snapshot, ManTechnologist), "Man technologist should be present");

        TestCaptureHelper.Capture(test.Terminal, "zwj-sequences");
    }

    /// <summary>
    /// Renders flag emoji (regional indicator pairs).
    /// </summary>
    [Fact]
    public void FlagEmoji_RendersCorrectly()
    {
        using var test = new TestTerminal(70, 18);

        test.Write("Flag Emoji (Regional Indicators)\n");
        test.Write("================================\n\n");

        test.Write($"Americas:  {FlagUS} 🇨🇦 🇲🇽 🇧🇷 🇦🇷 🇨🇴\n");
        test.Write($"Europe:    {FlagUK} {FlagDE} {FlagFR} 🇮🇹 🇪🇸 🇳🇱\n");
        test.Write($"Asia:      {FlagJP} 🇨🇳 🇰🇷 🇮🇳 🇹🇭 🇻🇳\n");
        test.Write($"Oceania:   {FlagAU} 🇳🇿 🇫🇯 🇵🇬 🇼🇸 🇹🇴\n");
        test.Write($"Africa:    🇿🇦 🇪🇬 🇳🇬 🇰🇪 🇪🇹 🇬🇭\n");
        test.Write($"Other:     🇺🇳 🏳️ 🏴 🏁 🚩 🎌\n");

        test.Write("\nNote: Flags are pairs of regional indicator symbols\n");

        var snapshot = test.Terminal.CreateSnapshot();

        Assert.True(ContainsGrapheme(snapshot, FlagUS), "US flag should be present");
        Assert.True(ContainsGrapheme(snapshot, FlagJP), "Japan flag should be present");

        TestCaptureHelper.Capture(test.Terminal, "flag-emoji");
    }

    /// <summary>
    /// Renders keycap sequences and symbol emoji.
    /// </summary>
    [Fact]
    public void KeycapsAndSymbols_RendersCorrectly()
    {
        using var test = new TestTerminal(60, 16);

        test.Write("Keycap Sequences and Symbols\n");
        test.Write("============================\n\n");

        test.Write($"Keycaps:  {Keycap0} {Keycap1} {Keycap2} 3️⃣ 4️⃣ 5️⃣ 6️⃣ 7️⃣ 8️⃣ 9️⃣ 🔟\n");
        test.Write($"Special:  {KeycapHash} {KeycapStar}\n");

        test.Write("\nSymbols:\n");
        test.Write("  Arrows:   ⬆️ ⬇️ ⬅️ ➡️ ↗️ ↘️ ↙️ ↖️ ↕️ ↔️\n");
        test.Write("  Shapes:   🔴 🟠 🟡 🟢 🔵 🟣 ⚫ ⚪ 🟤 🔶 🔷\n");
        test.Write("  Signs:    ⚠️ ⛔ 🚫 🔞 📵 🚭 🚯 🚱 🚳 🚷\n");
        test.Write("  Misc:     ℹ️ ⚡ 💯 ♻️ ✳️ ❇️ ‼️ ⁉️ ❓ ❗\n");

        var snapshot = test.Terminal.CreateSnapshot();

        Assert.True(ContainsGrapheme(snapshot, Keycap1), "Keycap 1 should be present");

        TestCaptureHelper.Capture(test.Terminal, "keycaps-and-symbols");
    }

    /// <summary>
    /// Tests emoji mixed with regular text and ANSI colors.
    /// </summary>
    [Fact]
    public void EmojiWithTextAndColors_RendersCorrectly()
    {
        using var test = new TestTerminal(70, 20);

        test.Write("Emoji Mixed with Text and Colors\n");
        test.Write("================================\n\n");

        // Emoji in colored text
        test.Write("\x1b[31mRed heart: ❤️ Love\x1b[0m\n");
        test.Write("\x1b[32mGreen check: ✅ Success\x1b[0m\n");
        test.Write("\x1b[33mYellow warning: ⚠️ Caution\x1b[0m\n");
        test.Write("\x1b[34mBlue rocket: 🚀 Launch\x1b[0m\n");
        test.Write("\x1b[35mMagenta sparkles: ✨ Magic\x1b[0m\n");

        test.Write("\n");

        // Status messages with emoji
        test.Write("Status Messages:\n");
        test.Write("  \x1b[42;30m PASS \x1b[0m ✅ All tests passed\n");
        test.Write("  \x1b[41;37m FAIL \x1b[0m ❌ 3 tests failed\n");
        test.Write("  \x1b[43;30m WARN \x1b[0m ⚠️ Deprecation warning\n");
        test.Write("  \x1b[44;37m INFO \x1b[0m ℹ️ Build started\n");

        test.Write("\n");

        // Emoji bullet points
        test.Write("Task List:\n");
        test.Write("  ✅ Complete documentation\n");
        test.Write("  ✅ Write unit tests\n");
        test.Write("  ⏳ Code review pending\n");
        test.Write("  ❌ Deploy to production\n");

        TestCaptureHelper.Capture(test.Terminal, "emoji-with-text-and-colors");
    }

    /// <summary>
    /// Tests emoji display width handling (most emoji are width 2).
    /// </summary>
    [Fact]
    public void EmojiDisplayWidth_RendersCorrectly()
    {
        using var test = new TestTerminal(80, 20);

        test.Write("Emoji Display Width Test\n");
        test.Write("========================\n\n");

        // Width verification - each emoji should be 2 cells wide
        test.Write("Width Grid (each cell pair marked with |):\n");
        test.Write("|😀|😃|😄|😁|😆|😅|🤣|😂|🙂|🙃|\n");
        test.Write("|--|--|--|--|--|--|--|--|--|--|\n");

        test.Write("\nAlignment Test:\n");
        test.Write("AB😀CD  <- 'CD' should align with 'CD' below\n");
        test.Write("AB--CD  <- Two dashes = one emoji width\n");

        test.Write("\nMixed Width:\n");
        test.Write("A🚀B🎉C✨D\n");
        test.Write("A--B--C--D  <- Each -- = emoji position\n");

        test.Write("\nTable with Emoji:\n");
        test.Write("┌────────┬──────┬────────┐\n");
        test.Write("│ Status │ Icon │ Count  │\n");
        test.Write("├────────┼──────┼────────┤\n");
        test.Write("│ Pass   │  ✅  │   42   │\n");
        test.Write("│ Fail   │  ❌  │    3   │\n");
        test.Write("│ Skip   │  ⏭️  │    5   │\n");
        test.Write("└────────┴──────┴────────┘\n");

        TestCaptureHelper.Capture(test.Terminal, "emoji-display-width");
    }

    /// <summary>
    /// Tests a comprehensive emoji showcase.
    /// </summary>
    [Fact]
    public void EmojiShowcase_RendersCorrectly()
    {
        using var test = new TestTerminal(80, 30);

        test.Write("╔══════════════════════════════════════════════════════════════════════════╗\n");
        test.Write("║                        EMOJI RENDERING SHOWCASE                          ║\n");
        test.Write("╚══════════════════════════════════════════════════════════════════════════╝\n\n");

        test.Write("┌─ Smileys & Emotion ─────────────────────────────────────────────────────┐\n");
        test.Write("│ 😀 😃 😄 😁 😆 😅 🤣 😂 🙂 🙃 😉 😊 😇 🥰 😍 🤩 😘 😗 ☺️ 😚 😋 😛 │\n");
        test.Write("└──────────────────────────────────────────────────────────────────────────┘\n\n");

        test.Write("┌─ People & Gestures ─────────────────────────────────────────────────────┐\n");
        test.Write("│ 👋 🤚 🖐️ ✋ 🖖 👌 🤌 🤏 ✌️ 🤞 🤟 🤘 🤙 👈 👉 👆 🖕 👇 ☝️ 👍 👎 ✊ │\n");
        test.Write("│ 👊 🤛 🤜 👏 🙌 👐 🤲 🙏 ✍️ 💅 🤳 💪 🦾 🦿 🦵 🦶 👂 🦻 👃 🧠 🫀 🫁 │\n");
        test.Write("└──────────────────────────────────────────────────────────────────────────┘\n\n");

        test.Write("┌─ Animals & Nature ──────────────────────────────────────────────────────┐\n");
        test.Write("│ 🐶 🐱 🐭 🐹 🐰 🦊 🐻 🐼 🐨 🐯 🦁 🐮 🐷 🐸 🐵 🙈 🙉 🙊 🐒 🐔 🐧 🐦 │\n");
        test.Write("│ 🌸 🌺 🌻 🌹 🌷 🌼 💐 🌾 🌿 🍀 🍁 🍂 🍃 🌲 🌳 🌴 🌵 🌱 🌿 ☘️ 🍀 🌲 │\n");
        test.Write("└──────────────────────────────────────────────────────────────────────────┘\n\n");

        test.Write("┌─ Food & Drink ──────────────────────────────────────────────────────────┐\n");
        test.Write("│ 🍏 🍎 🍐 🍊 🍋 🍌 🍉 🍇 🍓 🫐 🍈 🍒 🍑 🥭 🍍 🥥 🥝 🍅 🍆 🥑 🥦 🥬 │\n");
        test.Write("│ 🍔 🍟 🍕 🌭 🥪 🌮 🌯 🫔 🥙 🧆 🥚 🍳 🥘 🍲 🫕 🥣 🥗 🍿 🧈 🧂 🥫 🍝 │\n");
        test.Write("└──────────────────────────────────────────────────────────────────────────┘\n\n");

        test.Write("┌─ Travel & Places ─────────────────────────────────────────────────────────┐\n");
        test.Write("│ 🚗 🚕 🚙 🚌 🚎 🏎️ 🚓 🚑 🚒 🚐 🛻 🚚 🚛 🚜 🏍️ 🛵 🚲 🛴 🛹 🛼 🚁 ✈️ │\n");
        test.Write("│ 🏠 🏡 🏢 🏣 🏤 🏥 🏦 🏨 🏩 🏪 🏫 🏬 🏭 🏯 🏰 💒 🗼 🗽 ⛪ 🕌 🛕 🕍 │\n");
        test.Write("└──────────────────────────────────────────────────────────────────────────┘\n");

        TestCaptureHelper.Capture(test.Terminal, "emoji-showcase");
    }

    /// <summary>
    /// Tests that overwriting part of a wide character causes visual truncation.
    /// When a character is written over the continuation cell of a wide emoji,
    /// the emoji should be partially obscured (truncated) by the newer content.
    /// </summary>
    [Fact]
    public void WideCharacterTruncation_RendersCorrectly()
    {
        using var test = new TestTerminal(40, 16);

        test.Write("Wide Character Truncation Test\n");
        test.Write("==============================\n\n");

        // Scenario 1: Emoji followed by space, then overwrite second cell
        test.Write("Before: 😀 A  (emoji + space + A)\n");
        
        // Write emoji at column 0, it takes columns 0-1
        test.Write("\x1b[6;1H");  // Move to row 6, col 1
        test.Write("😀");         // Emoji at cols 0-1
        test.Write(" ");          // Space at col 2
        test.Write("A");          // A at col 3
        
        // Now move back and write X at column 1 (the continuation cell)
        test.Write("\x1b[7;1H");  // Row 7
        test.Write("😀");         // Emoji at cols 0-1
        test.Write(" ");          // Space at col 2  
        test.Write("A");          // A at col 3
        test.Write("\x1b[7;2H");  // Move to col 2 (1-indexed = col 1 in 0-indexed)
        test.Write("X");          // Overwrite the continuation cell - should truncate emoji

        // Row 8: Multiple emoji with overwrites
        test.Write("\x1b[9;1H");
        test.Write("Row 9: 🚀🔥⭐ <- three emoji\n");
        
        test.Write("\x1b[10;1H");
        test.Write("🚀🔥⭐");     // Three emoji
        test.Write("\x1b[10;3H"); // Move to middle of second emoji
        test.Write("XX");         // Overwrite - should truncate rocket and fire

        // Explanation
        test.Write("\x1b[12;1H");
        test.Write("The X chars should partially obscure\n");
        test.Write("the emoji they overlap. Older content\n");
        test.Write("renders first, newer content on top.\n");

        var snapshot = test.Terminal.CreateSnapshot();

        // Verify the X was written
        var cell = snapshot.GetCell(1, 6); // Row 7 (0-indexed=6), Col 2 (0-indexed=1)
        Assert.Equal("X", cell.Character);

        // Verify sequence numbers are being assigned
        Assert.True(cell.Sequence > 0, "Cell should have a sequence number");

        TestCaptureHelper.Capture(test.Terminal, "wide-char-truncation");
    }

    /// <summary>
    /// Tests sequence ordering in SVG - cells written later should appear on top.
    /// </summary>
    [Fact]
    public void SequenceOrdering_NewerContentOnTop()
    {
        using var test = new TestTerminal(30, 10);

        test.Write("Sequence Z-Order Test\n");
        test.Write("=====================\n\n");

        // Write a wide emoji
        test.Write("\x1b[5;1H");
        test.Write("😀😀😀😀😀"); // 5 emoji = 10 columns

        // Now overwrite parts with single characters
        test.Write("\x1b[5;3H");  // Col 3 (middle of second emoji)
        test.Write("AB");
        
        test.Write("\x1b[5;8H");  // Col 8 (middle of fourth emoji)
        test.Write("XY");

        test.Write("\x1b[7;1H");
        test.Write("AB and XY should overlap emoji\n");

        var snapshot = test.Terminal.CreateSnapshot();

        // Get cells and verify sequence ordering
        var emojiCell = snapshot.GetCell(0, 4); // First emoji
        var overwriteCell = snapshot.GetCell(2, 4); // 'A' that was written later

        Assert.True(overwriteCell.Sequence > emojiCell.Sequence, 
            "Overwrite should have higher sequence than original emoji");

        TestCaptureHelper.Capture(test.Terminal, "sequence-ordering");
    }

    #region Helper Methods

    /// <summary>
    /// Checks if the snapshot contains the specified grapheme cluster.
    /// </summary>
    private static bool ContainsGrapheme(Hex1bTerminalSnapshot snapshot, string grapheme)
    {
        for (int y = 0; y < snapshot.Height; y++)
        {
            for (int x = 0; x < snapshot.Width; x++)
            {
                var cell = snapshot.GetCell(x, y);
                if (cell.Character == grapheme)
                    return true;
            }
        }
        return false;
    }

    #endregion
}
