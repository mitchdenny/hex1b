using System.Globalization;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Comprehensive tests for Unicode character rendering and border alignment.
/// 
/// These tests verify that when exotic characters (wide characters, emoji, combining
/// characters, etc.) are placed inside bordered containers, the borders remain
/// properly aligned. Misalignment indicates bugs in:
/// - Display width calculation
/// - Cursor positioning after wide characters
/// - ANSI escape sequence handling
/// - Grapheme cluster segmentation
/// 
/// The test strategy:
/// 1. Create a bordered container with multiple lines, each containing a test character
/// 2. Render to the terminal once per category (not per character)
/// 3. Verify the right border column is consistent across all rows
/// </summary>
public class UnicodeBorderAlignmentTests
{
    private const int TerminalWidth = 80;
    private const int TerminalHeight = 30;
    private const int BorderWidth = 50;
    
    #region Test Character Categories
    
    // Basic ASCII (baseline - should always work)
    private static readonly string[] AsciiChars = ["A", "Z", "0", "9", "!", "@", "#", "$", "%"];
    
    // Simple emoji (surrogate pairs, display width 2)
    private static readonly string[] SimpleEmoji = [
        "😀", "😎", "🎉", "🔥", "🚀", "✅", "❌", "⭐", "💡", "🎯",
        "📋", "📁", "📄", "🔔", "🔒", "🔓", "⚡", "💻", "🖥️", "📱"
    ];
    
    // Emoji with skin tone modifiers
    private static readonly string[] SkinToneEmoji = [
        "👍🏻", "👍🏼", "👍🏽", "👍🏾", "👍🏿",
        "👋🏻", "👋🏼", "👋🏽", "👋🏾", "👋🏿",
        "🙋🏻", "🙋🏼", "🙋🏽", "🙋🏾", "🙋🏿"
    ];
    
    // ZWJ sequences (families, professions)
    private static readonly string[] ZwjEmoji = [
        "👨‍👩‍👧", "👨‍👩‍👧‍👦", "👩‍❤️‍👨", "👨‍💻", "👩‍🔬",
        "👩‍🚀", "👨‍🍳", "👩‍🎨", "🧑‍💼", "👨‍🔧"
    ];
    
    // Flag emoji (regional indicators)
    private static readonly string[] FlagEmoji = [
        "🇺🇸", "🇬🇧", "🇯🇵", "🇩🇪", "🇫🇷",
        "🇨🇦", "🇦🇺", "🇧🇷", "🇮🇳", "🇨🇳"
    ];
    
    // CJK characters (wide, display width 2)
    private static readonly string[] CjkChars = [
        "中", "文", "日", "本", "語",
        "한", "국", "어", "漢", "字",
        "東", "京", "北", "京", "上"
    ];
    
    // Combining characters (zero width modifiers)
    private static readonly string[] CombiningChars = [
        "e\u0301",      // é (e + combining acute)
        "n\u0303",      // ñ (n + combining tilde)
        "a\u030A",      // å (a + combining ring above)
        "o\u0308",      // ö (o + combining diaeresis)
        "u\u0302",      // û (u + combining circumflex)
        "c\u0327",      // ç (c + combining cedilla)
        "a\u0301\u0328",// ą́ (a + acute + ogonek)
        "s\u030C"       // š (s + combining caron)
    ];
    
    // Keycap sequences
    private static readonly string[] KeycapEmoji = [
        "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣",
        "6️⃣", "7️⃣", "8️⃣", "9️⃣", "0️⃣",
        "#️⃣", "*️⃣"
    ];
    
    // Variation selector sequences
    private static readonly string[] VariationSelectors = [
        "❤️",   // Heavy heart with variation selector
        "☀️",   // Sun with variation selector
        "⚠️",   // Warning with variation selector
        "✨",   // Sparkles
        "☁️",   // Cloud with variation selector
        "⭐",   // Star (no VS)
        "★",    // Black star
        "☆"     // White star
    ];
    
    // Box drawing characters
    private static readonly string[] BoxDrawing = [
        "─", "│", "┌", "┐", "└", "┘", "├", "┤", "┬", "┴", "┼",
        "═", "║", "╔", "╗", "╚", "╝", "╠", "╣", "╦", "╩", "╬"
    ];
    
    // Mathematical symbols and special characters
    private static readonly string[] MathSymbols = [
        "∞", "∑", "∏", "√", "∫", "≈", "≠", "≤", "≥", "±",
        "÷", "×", "∂", "∆", "∇", "∈", "∉", "⊂", "⊃", "∪"
    ];
    
    // Currency and other symbols
    private static readonly string[] CurrencySymbols = [
        "€", "£", "¥", "₹", "₽", "₿", "฿", "₩", "₴", "₸"
    ];
    
    // Greek letters
    private static readonly string[] GreekLetters = [
        "α", "β", "γ", "δ", "ε", "ζ", "η", "θ", "ι", "κ",
        "λ", "μ", "ν", "ξ", "π", "ρ", "σ", "τ", "υ", "φ"
    ];
    
    // Full-width characters (should be 2 cells wide)
    private static readonly string[] FullWidthChars = [
        "Ａ", "Ｂ", "Ｃ", "０", "１", "２", "！", "？"
    ];
    
    // Half-width katakana (narrow)
    private static readonly string[] HalfWidthKatakana = [
        "ｱ", "ｲ", "ｳ", "ｴ", "ｵ", "ｶ", "ｷ", "ｸ", "ｹ", "ｺ"
    ];
    
    // Thai characters (complex scripts with combining)
    private static readonly string[] ThaiChars = [
        "ก", "ข", "ค", "ง", "จ", "กิ", "กี", "กึ", "กื", "กุ"
    ];
    
    // Arabic characters (RTL, but we test LTR rendering)
    private static readonly string[] ArabicChars = [
        "ا", "ب", "ت", "ث", "ج", "ح", "خ", "د", "ذ", "ر"
    ];
    
    // Hebrew characters
    private static readonly string[] HebrewChars = [
        "א", "ב", "ג", "ד", "ה", "ו", "ז", "ח", "ט", "י"
    ];
    
    // Known problematic characters - regression tests for specific bugs
    // These are characters that have been observed to cause alignment issues
    private static readonly string[] KnownProblematicChars = [
        // Characters from test failures
        "✅",    // U+2705 - White Heavy Check Mark (Dingbats)
        "❌",    // U+274C - Cross Mark (Dingbats)
        "⭐",    // U+2B50 - White Medium Star (Misc Symbols and Arrows)
        "⚡",    // U+26A1 - High Voltage (Misc Symbols)
        "🖥️",   // U+1F5A5 + U+FE0F - Desktop Computer with VS16
        
        // Colored circles/shapes (commonly used in UIs)
        "🔴",    // U+1F534 - Red Circle
        "🟠",    // U+1F7E0 - Orange Circle
        "🟡",    // U+1F7E1 - Yellow Circle
        "🟢",    // U+1F7E2 - Green Circle
        "🔵",    // U+1F535 - Blue Circle
        "⚫",    // U+26AB - Black Circle (Misc Symbols)
        "⚪",    // U+26AA - White Circle (Misc Symbols)
        
        // Common status indicators
        "⚠️",   // U+26A0 + U+FE0F - Warning with VS16
        "ℹ️",   // U+2139 + U+FE0F - Info with VS16
        "❓",    // U+2753 - Question Mark Ornament
        "❗",    // U+2757 - Exclamation Mark
        
        // Arrows that may have issues
        "➡️",   // U+27A1 + U+FE0F - Right Arrow with VS16
        "⬆️",   // U+2B06 + U+FE0F - Up Arrow with VS16
        "⬇️",   // U+2B07 + U+FE0F - Down Arrow with VS16
        "⬅️",   // U+2B05 + U+FE0F - Left Arrow with VS16
    ];
    
    #endregion
    
    #region Border Alignment Test Helpers
    
    /// <summary>
    /// Tests that a character renders correctly inside a border.
    /// Verifies the right border is aligned on all rows.
    /// </summary>
    private async Task AssertBorderAlignmentAsync(
        string testChar, 
        string charDescription,
        CancellationToken cancellationToken = default)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(TerminalWidth, TerminalHeight)
            .Build();
        
        // Create a simple bordered text widget
        using var app = new Hex1bApp(
            ctx => ctx.Border(
                ctx.VStack(v => [
                    v.Text($"Test: {testChar}"),
                    v.Text($"Char: {testChar} padding"),
                    v.Text("End line")
                ])
            ).FixedWidth(BorderWidth),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(cancellationToken);
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("End line"), TimeSpan.FromSeconds(2), "render complete")
            .Capture()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, cancellationToken);
        
        await runTask;
        
        // Verify border alignment
        AssertRightBorderAligned(snapshot, charDescription, testChar);
    }
    
    /// <summary>
    /// Asserts that the right border character appears in the same column on all bordered rows.
    /// </summary>
    private void AssertRightBorderAligned(
        Hex1bTerminalSnapshot snapshot, 
        string charDescription,
        string testChar)
    {
        var text = snapshot.GetText();
        var lines = text.Split('\n');
        
        // Find the border character column on each line
        var borderPositions = new List<(int lineNum, int position)>();
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            if (string.IsNullOrEmpty(line)) continue;
            
            // Look for right border character (│ or similar)
            var borderChars = new[] { '│', '┐', '┘', '║', '╗', '╝' };
            
            // Find rightmost border character
            var rightBorderPos = -1;
            for (int j = line.Length - 1; j >= 0; j--)
            {
                if (borderChars.Contains(line[j]))
                {
                    rightBorderPos = j;
                    break;
                }
            }
            
            if (rightBorderPos >= 0)
            {
                borderPositions.Add((i, rightBorderPos));
            }
        }
        
        // All border positions should be the same
        if (borderPositions.Count < 2)
        {
            Assert.Fail($"Expected multiple border lines for '{charDescription}' ({testChar}). " +
                $"Found {borderPositions.Count} lines with borders.\n" +
                $"Screen:\n{text}");
        }
        
        var expectedPosition = borderPositions[0].position;
        var misaligned = borderPositions.Where(p => p.position != expectedPosition).ToList();
        
        if (misaligned.Count > 0)
        {
            var details = string.Join("\n", borderPositions.Select(p => 
                $"  Line {p.lineNum}: border at column {p.position}"));
            
            Assert.Fail(
                $"Border misalignment detected for '{charDescription}' (char: {testChar})\n" +
                $"Expected all borders at column {expectedPosition}, but found:\n{details}\n\n" +
                $"Screen:\n{text}");
        }
    }
    
    /// <summary>
    /// Tests multiple characters in a single app render - much more efficient.
    /// Each character gets its own line in the border, then we check alignment.
    /// </summary>
    private async Task AssertBorderAlignmentBatchAsync(
        string[] chars,
        string categoryName,
        CancellationToken cancellationToken = default)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(TerminalWidth, TerminalHeight)
            .Build();
        
        // Build lines with each character, plus a baseline
        var lines = new List<string> { "BASELINE: Plain ASCII reference" };
        foreach (var chr in chars)
        {
            lines.Add($"Test {chr} char");
        }
        lines.Add("END MARKER");
        
        using var app = new Hex1bApp(
            ctx => ctx.Border(
                ctx.VStack(v => lines.Select(line => v.Text(line)).ToArray())
            ).FixedWidth(BorderWidth),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(cancellationToken);
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("END MARKER"), TimeSpan.FromSeconds(2), "render complete")
            .Capture()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, cancellationToken);
        
        await runTask;
        
        // Check alignment
        AssertRightBorderAlignedWithDetails(snapshot, categoryName, chars);
    }
    
    /// <summary>
    /// Asserts border alignment and reports which specific characters caused issues.
    /// </summary>
    private void AssertRightBorderAlignedWithDetails(
        Hex1bTerminalSnapshot snapshot, 
        string categoryName,
        string[] testChars)
    {
        var text = snapshot.GetText();
        var lines = text.Split('\n');
        
        var borderPositions = new List<(int lineNum, int position, string lineContent)>();
        var borderChars = new[] { '│', '┐', '┘', '║', '╗', '╝' };
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            if (string.IsNullOrEmpty(line)) continue;
            
            var rightBorderPos = -1;
            for (int j = line.Length - 1; j >= 0; j--)
            {
                if (borderChars.Contains(line[j]))
                {
                    rightBorderPos = j;
                    break;
                }
            }
            
            if (rightBorderPos >= 0)
            {
                borderPositions.Add((i, rightBorderPos, line));
            }
        }
        
        if (borderPositions.Count < 2)
        {
            Assert.Fail($"Expected multiple border lines for '{categoryName}'. " +
                $"Found {borderPositions.Count}.\nScreen:\n{text}");
        }
        
        var expectedPosition = borderPositions[0].position;
        var misaligned = borderPositions
            .Where(p => p.position != expectedPosition)
            .ToList();
        
        if (misaligned.Count > 0)
        {
            // Find which test characters caused the misalignment
            var failedChars = new List<string>();
            foreach (var m in misaligned)
            {
                foreach (var chr in testChars)
                {
                    if (m.lineContent.Contains(chr))
                    {
                        failedChars.Add($"'{chr}' (U+{GetCodepoints(chr)}) - border at col {m.position}");
                        break;
                    }
                }
            }
            
            var details = string.Join("\n", misaligned.Select(p => 
                $"  Line {p.lineNum} (col {p.position}): {p.lineContent.Substring(0, Math.Min(50, p.lineContent.Length))}..."));
            
            Assert.Fail(
                $"Border misalignment in '{categoryName}'\n" +
                $"Expected all borders at column {expectedPosition}\n\n" +
                $"Failed characters:\n{string.Join("\n", failedChars)}\n\n" +
                $"Misaligned lines:\n{details}\n\n" +
                $"Full screen:\n{text}");
        }
    }
    
    private static string GetCodepoints(string s)
    {
        var codepoints = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(s);
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            foreach (var rune in element.EnumerateRunes())
            {
                codepoints.Add(rune.Value.ToString("X4"));
            }
        }
        return string.Join("+", codepoints);
    }
    
    #endregion
    
    #region Category Tests
    
    [Fact]
    public async Task BorderAlignment_AsciiCharacters_AlignCorrectly()
    {
        await AssertBorderAlignmentBatchAsync(AsciiChars, "ASCII", TestContext.Current.CancellationToken);
    }
    
    [Fact]
    public async Task BorderAlignment_KnownProblematicChars_AlignCorrectly()
    {
        // This is the most important test - these are characters that have been
        // observed to cause real-world alignment issues in the FullAppDemo
        await AssertBorderAlignmentBatchAsync(KnownProblematicChars, "Known Problematic", TestContext.Current.CancellationToken);
    }
    
    [Fact]
    public async Task BorderAlignment_SimpleEmoji_AlignCorrectly()
    {
        await AssertBorderAlignmentBatchAsync(SimpleEmoji, "Simple Emoji", TestContext.Current.CancellationToken);
    }
    
    [Fact]
    public async Task BorderAlignment_SkinToneEmoji_AlignCorrectly()
    {
        await AssertBorderAlignmentBatchAsync(SkinToneEmoji, "Skin Tone Emoji", TestContext.Current.CancellationToken);
    }
    
    [Fact]
    public async Task BorderAlignment_ZwjEmoji_AlignCorrectly()
    {
        await AssertBorderAlignmentBatchAsync(ZwjEmoji, "ZWJ Emoji", TestContext.Current.CancellationToken);
    }
    
    [Fact]
    public async Task BorderAlignment_FlagEmoji_AlignCorrectly()
    {
        await AssertBorderAlignmentBatchAsync(FlagEmoji, "Flag Emoji", TestContext.Current.CancellationToken);
    }
    
    [Fact]
    public async Task BorderAlignment_CjkCharacters_AlignCorrectly()
    {
        await AssertBorderAlignmentBatchAsync(CjkChars, "CJK", TestContext.Current.CancellationToken);
    }
    
    [Fact]
    public async Task BorderAlignment_CombiningCharacters_AlignCorrectly()
    {
        await AssertBorderAlignmentBatchAsync(CombiningChars, "Combining Characters", TestContext.Current.CancellationToken);
    }
    
    [Fact]
    public async Task BorderAlignment_KeycapEmoji_AlignCorrectly()
    {
        await AssertBorderAlignmentBatchAsync(KeycapEmoji, "Keycap Emoji", TestContext.Current.CancellationToken);
    }
    
    [Fact]
    public async Task BorderAlignment_VariationSelectors_AlignCorrectly()
    {
        await AssertBorderAlignmentBatchAsync(VariationSelectors, "Variation Selectors", TestContext.Current.CancellationToken);
    }
    
    [Fact]
    public async Task BorderAlignment_BoxDrawing_AlignCorrectly()
    {
        await AssertBorderAlignmentBatchAsync(BoxDrawing, "Box Drawing", TestContext.Current.CancellationToken);
    }
    
    [Fact]
    public async Task BorderAlignment_MathSymbols_AlignCorrectly()
    {
        await AssertBorderAlignmentBatchAsync(MathSymbols, "Math Symbols", TestContext.Current.CancellationToken);
    }
    
    [Fact]
    public async Task BorderAlignment_CurrencySymbols_AlignCorrectly()
    {
        await AssertBorderAlignmentBatchAsync(CurrencySymbols, "Currency Symbols", TestContext.Current.CancellationToken);
    }
    
    [Fact]
    public async Task BorderAlignment_GreekLetters_AlignCorrectly()
    {
        await AssertBorderAlignmentBatchAsync(GreekLetters, "Greek Letters", TestContext.Current.CancellationToken);
    }
    
    [Fact]
    public async Task BorderAlignment_FullWidthCharacters_AlignCorrectly()
    {
        await AssertBorderAlignmentBatchAsync(FullWidthChars, "Full Width", TestContext.Current.CancellationToken);
    }
    
    [Fact]
    public async Task BorderAlignment_HalfWidthKatakana_AlignCorrectly()
    {
        await AssertBorderAlignmentBatchAsync(HalfWidthKatakana, "Half Width Katakana", TestContext.Current.CancellationToken);
    }
    
    [Fact]
    public async Task BorderAlignment_ThaiCharacters_AlignCorrectly()
    {
        await AssertBorderAlignmentBatchAsync(ThaiChars, "Thai", TestContext.Current.CancellationToken);
    }
    
    [Fact]
    public async Task BorderAlignment_ArabicCharacters_AlignCorrectly()
    {
        await AssertBorderAlignmentBatchAsync(ArabicChars, "Arabic", TestContext.Current.CancellationToken);
    }
    
    [Fact]
    public async Task BorderAlignment_HebrewCharacters_AlignCorrectly()
    {
        await AssertBorderAlignmentBatchAsync(HebrewChars, "Hebrew", TestContext.Current.CancellationToken);
    }
    
    #endregion
    
    #region Mixed Content Tests
    
    [Fact]
    public async Task BorderAlignment_MixedAsciiAndEmoji_AlignCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(TerminalWidth, TerminalHeight)
            .Build();
        
        using var app = new Hex1bApp(
            ctx => ctx.Border(
                ctx.VStack(v => [
                    v.Text("Status: 📋 Tasks"),
                    v.Text("Files: 📁 Documents"),
                    v.Text("Alert: 🔔 New message"),
                    v.Text("Plain ASCII text")
                ])
            ).FixedWidth(BorderWidth),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Plain ASCII"), TimeSpan.FromSeconds(2), "render complete")
            .Capture()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        AssertRightBorderAligned(snapshot, "Mixed ASCII and Emoji", "mixed");
    }
    
    [Fact]
    public async Task BorderAlignment_MultipleEmojiPerLine_AlignCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(TerminalWidth, TerminalHeight)
            .Build();
        
        using var app = new Hex1bApp(
            ctx => ctx.Border(
                ctx.VStack(v => [
                    v.Text("🔥🚀✅ Hot rocket check"),
                    v.Text("📋📁📄 Files galore"),
                    v.Text("Plain line for compare"),
                    v.Text("🇺🇸🇬🇧🇯🇵 Flags")
                ])
            ).FixedWidth(BorderWidth),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Plain line"), TimeSpan.FromSeconds(2), "render complete")
            .Capture()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        AssertRightBorderAligned(snapshot, "Multiple Emoji Per Line", "multiple emoji");
    }
    
    [Fact]
    public async Task BorderAlignment_CjkMixedWithAscii_AlignCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(TerminalWidth, TerminalHeight)
            .Build();
        
        using var app = new Hex1bApp(
            ctx => ctx.Border(
                ctx.VStack(v => [
                    v.Text("Title: 東京タワー"),
                    v.Text("日本語 and English"),
                    v.Text("Plain ASCII only"),
                    v.Text("中文混合text")
                ])
            ).FixedWidth(BorderWidth),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Plain ASCII"), TimeSpan.FromSeconds(2), "render complete")
            .Capture()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        AssertRightBorderAligned(snapshot, "CJK Mixed With ASCII", "CJK mixed");
    }
    
    #endregion
    
    #region Edge Cases
    
    [Fact]
    public async Task BorderAlignment_EmptyString_AlignCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(TerminalWidth, TerminalHeight)
            .Build();
        
        using var app = new Hex1bApp(
            ctx => ctx.Border(
                ctx.VStack(v => [
                    v.Text("First line"),
                    v.Text(""),
                    v.Text("Third line")
                ])
            ).FixedWidth(BorderWidth),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Third line"), TimeSpan.FromSeconds(2), "render complete")
            .Capture()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        AssertRightBorderAligned(snapshot, "Empty String", "empty");
    }
    
    [Fact]
    public async Task BorderAlignment_OnlySpaces_AlignCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(TerminalWidth, TerminalHeight)
            .Build();
        
        using var app = new Hex1bApp(
            ctx => ctx.Border(
                ctx.VStack(v => [
                    v.Text("First line"),
                    v.Text("     "),
                    v.Text("Third line")
                ])
            ).FixedWidth(BorderWidth),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Third line"), TimeSpan.FromSeconds(2), "render complete")
            .Capture()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        AssertRightBorderAligned(snapshot, "Only Spaces", "spaces");
    }
    
    [Fact]
    public async Task BorderAlignment_LongTextWithEmoji_AlignCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(TerminalWidth, TerminalHeight)
            .Build();
        
        using var app = new Hex1bApp(
            ctx => ctx.Border(
                ctx.VStack(v => [
                    v.Text("🔥 This is a longer line with emoji at start"),
                    v.Text("This line has emoji at the end 🚀"),
                    v.Text("Middle 💡 emoji in this line"),
                    v.Text("No emoji baseline reference")
                ])
            ).FixedWidth(50),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("baseline"), TimeSpan.FromSeconds(2), "render complete")
            .Capture()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        AssertRightBorderAligned(snapshot, "Long Text With Emoji", "long emoji");
    }
    
    #endregion
    
    #region Unicode Range Exhaustive Tests
    
    /// <summary>
    /// Tests a range of Unicode codepoints for border alignment.
    /// Uses batch testing - all characters in a range are tested in a single render.
    /// </summary>
    [Theory]
    [InlineData(0x0080, 0x00FF, "Latin-1 Supplement")]           // Accented chars, symbols
    [InlineData(0x0100, 0x017F, "Latin Extended-A")]             // European Latin
    [InlineData(0x0370, 0x03FF, "Greek and Coptic")]             // Greek
    [InlineData(0x0400, 0x04FF, "Cyrillic")]                     // Russian, etc.
    [InlineData(0x2000, 0x206F, "General Punctuation")]          // Special spaces, dashes
    [InlineData(0x2190, 0x21FF, "Arrows")]                       // Arrow symbols
    [InlineData(0x2200, 0x22FF, "Mathematical Operators")]       // Math symbols
    [InlineData(0x2300, 0x23FF, "Miscellaneous Technical")]      // Technical symbols
    [InlineData(0x2500, 0x257F, "Box Drawing")]                  // Box chars
    [InlineData(0x2580, 0x259F, "Block Elements")]               // Block chars
    [InlineData(0x25A0, 0x25FF, "Geometric Shapes")]             // Shapes
    [InlineData(0x2600, 0x26FF, "Miscellaneous Symbols")]        // Misc symbols
    [InlineData(0x2700, 0x27BF, "Dingbats")]                     // Dingbats
    [InlineData(0x3000, 0x303F, "CJK Symbols and Punctuation")]  // CJK punctuation
    [InlineData(0x4E00, 0x4E50, "CJK Unified Ideographs (sample)")] // CJK chars (sample)
    public async Task BorderAlignment_UnicodeRange_AlignCorrectly(
        int startCodepoint, 
        int endCodepoint, 
        string rangeName)
    {
        // Collect sample characters from the range (every 8th to keep count reasonable)
        var chars = new List<string>();
        for (int cp = startCodepoint; cp <= endCodepoint; cp += 8)
        {
            // Skip control characters, unassigned, and format chars
            try
            {
                var category = char.GetUnicodeCategory((char)cp);
                if (category == UnicodeCategory.Control ||
                    category == UnicodeCategory.OtherNotAssigned ||
                    category == UnicodeCategory.Format ||
                    category == UnicodeCategory.PrivateUse)
                {
                    continue;
                }
                
                chars.Add(char.ConvertFromUtf32(cp));
            }
            catch
            {
                // Skip invalid codepoints
            }
        }
        
        if (chars.Count == 0)
        {
            // No valid characters in this range
            return;
        }
        
        // Test all characters in a single batch render
        await AssertBorderAlignmentBatchAsync(chars.ToArray(), rangeName, TestContext.Current.CancellationToken);
    }
    
    #endregion
}
