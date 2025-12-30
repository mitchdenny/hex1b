using Hex1b.Terminal;
using Hex1b.Terminal.Automation;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests that render a visual matrix of cell attribute combinations and capture SVGs.
/// </summary>
public class CellAttributeMatrixTests
{
    /// <summary>
    /// Simple synchronous test terminal that directly calls ApplyTokens.
    /// </summary>
    private sealed class TestTerminal : IDisposable
    {
        private readonly StreamWorkloadAdapter _workload;

        public Hex1bTerminal Terminal { get; }

        public TestTerminal(int width, int height)
        {
            // Use memory streams - we don't actually need them for this approach
            _workload = StreamWorkloadAdapter.CreateHeadless(width, height);
            Terminal = new Hex1bTerminal(_workload, width, height);
        }

        /// <summary>
        /// Write ANSI sequences directly to the terminal.
        /// </summary>
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

    /// <summary>
    /// Finds the first cell matching the specified character that has the expected attributes.
    /// </summary>
    private static bool HasCellWithAttributes(
        Hex1bTerminalSnapshot snapshot,
        char c,
        CellAttributes expectedAttrs)
    {
        var target = c.ToString();
        for (int y = 0; y < snapshot.Height; y++)
        {
            for (int x = 0; x < snapshot.Width; x++)
            {
                var cell = snapshot.GetCell(x, y);
                if (cell.Character == target && cell.Attributes == expectedAttrs)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Renders individual attributes each on their own line.
    /// </summary>
    [Fact]
    public void IndividualAttributes_RendersCorrectly()
    {
        using var test = new TestTerminal(40, 25);

        // Write test characters with unique attributes on first row
        test.Write("\x1b[0mN\x1b[0m");          // Normal
        test.Write("\x1b[1mB\x1b[0m");          // Bold
        test.Write("\x1b[2mD\x1b[0m");          // Dim
        test.Write("\x1b[3mI\x1b[0m");          // Italic
        test.Write("\x1b[4mU\x1b[0m");          // Underline
        test.Write("\x1b[5mK\x1b[0m");          // blinK
        test.Write("\x1b[7mR\x1b[0m");          // Reverse
        test.Write("\x1b[8mH\x1b[0m");          // Hidden
        test.Write("\x1b[9mS\x1b[0m");          // Strikethrough
        test.Write("\x1b[53mO\x1b[0m");         // Overline
        test.Write("\n\n");

        // Display version with sample text
        test.Write("Cell Attribute Samples\n");
        test.Write("======================\n\n");
        test.Write("\x1b[0mNormal:        \x1b[0mSample Text\x1b[0m\n");
        test.Write("\x1b[0mBold:          \x1b[1mSample Text\x1b[0m\n");
        test.Write("\x1b[0mDim:           \x1b[2mSample Text\x1b[0m\n");
        test.Write("\x1b[0mItalic:        \x1b[3mSample Text\x1b[0m\n");
        test.Write("\x1b[0mUnderline:     \x1b[4mSample Text\x1b[0m\n");
        test.Write("\x1b[0mBlink:         \x1b[5mSample Text\x1b[0m\n");
        test.Write("\x1b[0mReverse:       \x1b[7mSample Text\x1b[0m\n");
        test.Write("\x1b[0mHidden:        \x1b[8mSample Text\x1b[0m\n");
        test.Write("\x1b[0mStrikethrough: \x1b[9mSample Text\x1b[0m\n");
        test.Write("\x1b[0mOverline:      \x1b[53mSample Text\x1b[0m\n");

        // Verify attributes via first row's single chars
        var snapshot = test.Terminal.CreateSnapshot();

        Assert.True(HasCellWithAttributes(snapshot, 'N', CellAttributes.None), "Normal");
        Assert.True(HasCellWithAttributes(snapshot, 'B', CellAttributes.Bold), "Bold");
        Assert.True(HasCellWithAttributes(snapshot, 'D', CellAttributes.Dim), "Dim");
        Assert.True(HasCellWithAttributes(snapshot, 'I', CellAttributes.Italic), "Italic");
        Assert.True(HasCellWithAttributes(snapshot, 'U', CellAttributes.Underline), "Underline");
        Assert.True(HasCellWithAttributes(snapshot, 'K', CellAttributes.Blink), "Blink");
        Assert.True(HasCellWithAttributes(snapshot, 'R', CellAttributes.Reverse), "Reverse");
        Assert.True(HasCellWithAttributes(snapshot, 'H', CellAttributes.Hidden), "Hidden");
        Assert.True(HasCellWithAttributes(snapshot, 'S', CellAttributes.Strikethrough), "Strikethrough");
        Assert.True(HasCellWithAttributes(snapshot, 'O', CellAttributes.Overline), "Overline");

        TestCaptureHelper.Capture(test.Terminal, "individual-attributes");
    }

    /// <summary>
    /// Renders common attribute combinations.
    /// </summary>
    [Fact]
    public void CommonCombinations_RendersCorrectly()
    {
        using var test = new TestTerminal(50, 25);  // Increased height

        // Write test patterns with unique characters for each combination
        test.Write("\x1b[1;3m1\x1b[0m"); // Bold+Italic
        test.Write("\x1b[1;4m2\x1b[0m"); // Bold+Underline
        test.Write("\x1b[1;9m3\x1b[0m"); // Bold+Strike
        test.Write("\x1b[3;4m4\x1b[0m"); // Italic+Underline
        test.Write("\x1b[3;9m5\x1b[0m"); // Italic+Strike
        test.Write("\x1b[4;9m6\x1b[0m"); // Underline+Strike
        test.Write("\x1b[1;3;4m7\x1b[0m"); // Bold+Italic+Underline
        test.Write("\x1b[1;3;4;9m8\x1b[0m"); // All four
        test.Write("\n\n");

        test.Write("Common Attribute Combinations\n");
        test.Write("==============================\n\n");

        // Two-attribute combinations
        test.Write("\x1b[0mBold+Italic:          \x1b[1;3mSample Text\x1b[0m\n");
        test.Write("\x1b[0mBold+Underline:       \x1b[1;4mSample Text\x1b[0m\n");
        test.Write("\x1b[0mBold+Strikethrough:   \x1b[1;9mSample Text\x1b[0m\n");
        test.Write("\x1b[0mItalic+Underline:     \x1b[3;4mSample Text\x1b[0m\n");
        test.Write("\x1b[0mItalic+Strikethrough: \x1b[3;9mSample Text\x1b[0m\n");
        test.Write("\x1b[0mUnderline+Strike:     \x1b[4;9mSample Text\x1b[0m\n");
        test.Write("\x1b[0mDim+Italic:           \x1b[2;3mSample Text\x1b[0m\n");
        test.Write("\x1b[0mReverse+Bold:         \x1b[7;1mSample Text\x1b[0m\n");

        test.Write("\n");

        // Three-attribute combinations
        test.Write("\x1b[0mBold+Italic+Underline:  \x1b[1;3;4mSample Text\x1b[0m\n");
        test.Write("\x1b[0mBold+Italic+Strike:     \x1b[1;3;9mSample Text\x1b[0m\n");
        test.Write("\x1b[0mBold+Under+Strike:      \x1b[1;4;9mSample Text\x1b[0m\n");
        test.Write("\x1b[0mItalic+Under+Strike:    \x1b[3;4;9mSample Text\x1b[0m\n");

        test.Write("\n");

        // Four-attribute combination
        test.Write("\x1b[0mBold+Ital+Under+Strike: \x1b[1;3;4;9mSample Text\x1b[0m\n");

        // Verify combinations via test characters
        var snapshot = test.Terminal.CreateSnapshot();

        Assert.True(HasCellWithAttributes(snapshot, '1', CellAttributes.Bold | CellAttributes.Italic), "Bold+Italic");
        Assert.True(HasCellWithAttributes(snapshot, '2', CellAttributes.Bold | CellAttributes.Underline), "Bold+Underline");
        Assert.True(HasCellWithAttributes(snapshot, '3', CellAttributes.Bold | CellAttributes.Strikethrough), "Bold+Strike");
        Assert.True(HasCellWithAttributes(snapshot, '4', CellAttributes.Italic | CellAttributes.Underline), "Italic+Underline");
        Assert.True(HasCellWithAttributes(snapshot, '5', CellAttributes.Italic | CellAttributes.Strikethrough), "Italic+Strike");
        Assert.True(HasCellWithAttributes(snapshot, '6', CellAttributes.Underline | CellAttributes.Strikethrough), "Underline+Strike");
        Assert.True(HasCellWithAttributes(snapshot, '7', CellAttributes.Bold | CellAttributes.Italic | CellAttributes.Underline), "Bold+Italic+Underline");
        Assert.True(HasCellWithAttributes(snapshot, '8', CellAttributes.Bold | CellAttributes.Italic | CellAttributes.Underline | CellAttributes.Strikethrough), "All four");

        TestCaptureHelper.Capture(test.Terminal, "common-combinations");
    }

    /// <summary>
    /// Renders attributes with colors.
    /// </summary>
    [Fact]
    public void AttributesWithColors_RendersCorrectly()
    {
        using var test = new TestTerminal(50, 16);

        test.Write("Attributes With Colors\n");
        test.Write("======================\n\n");

        // Bold with different colors
        test.Write("\x1b[1;31mBold Red\x1b[0m  ");
        test.Write("\x1b[1;32mBold Green\x1b[0m  ");
        test.Write("\x1b[1;34mBold Blue\x1b[0m\n");

        // Italic with different colors
        test.Write("\x1b[3;33mItalic Yellow\x1b[0m  ");
        test.Write("\x1b[3;35mItalic Magenta\x1b[0m  ");
        test.Write("\x1b[3;36mItalic Cyan\x1b[0m\n");

        // Underline with colors
        test.Write("\x1b[4;91mUnderline Bright Red\x1b[0m  ");
        test.Write("\x1b[4;92mUnderline Bright Green\x1b[0m\n");

        // Combined with background colors
        test.Write("\n");
        test.Write("\x1b[1;37;41m Bold White on Red \x1b[0m ");
        test.Write("\x1b[3;30;43m Italic Black on Yellow \x1b[0m\n");
        test.Write("\x1b[4;37;44m Underline White on Blue \x1b[0m ");
        test.Write("\x1b[7;32m Reverse Green \x1b[0m\n");

        // Strikethrough with colors
        test.Write("\n");
        test.Write("\x1b[9;31mStrikethrough Red\x1b[0m  ");
        test.Write("\x1b[9;1;34mStrike Bold Blue\x1b[0m\n");

        // All attributes with color
        test.Write("\n");
        test.Write("\x1b[1;3;4;9;95mBold Italic Underline Strike Magenta\x1b[0m\n");

        TestCaptureHelper.Capture(test.Terminal, "attributes-with-colors");
    }

    /// <summary>
    /// Renders a full matrix grid of attribute combinations.
    /// </summary>
    [Fact]
    public void FullAttributeMatrix_RendersCorrectly()
    {
        using var test = new TestTerminal(80, 30);

        test.Write("Attribute Combination Matrix\n");
        test.Write("============================\n\n");

        // Column headers (abbreviated)
        test.Write("           B  D  I  U  K  R  H  S  O\n");
        test.Write("          ld im ta nd ln ev id tr vr\n");
        test.Write("           d  m  l  r  k  s  n  k  l\n");
        test.Write("\n");

        // Generate rows for single and dual attribute combinations
        var attributes = new (string name, int code, CellAttributes attr)[]
        {
            ("Bold", 1, CellAttributes.Bold),
            ("Dim", 2, CellAttributes.Dim),
            ("Italic", 3, CellAttributes.Italic),
            ("Underline", 4, CellAttributes.Underline),
            ("Blink", 5, CellAttributes.Blink),
            ("Reverse", 7, CellAttributes.Reverse),
            ("Hidden", 8, CellAttributes.Hidden),
            ("Strike", 9, CellAttributes.Strikethrough),
            ("Overline", 53, CellAttributes.Overline),
        };

        foreach (var (name, code, attr) in attributes)
        {
            test.Write($"{name,-10} ");
            test.Write($"\x1b[{code}mABC\x1b[0m ");

            // Show combined with each other attribute
            foreach (var (_, otherCode, _) in attributes)
            {
                if (otherCode == code)
                {
                    test.Write("  - ");
                }
                else
                {
                    test.Write($"\x1b[{code};{otherCode}mX\x1b[0m  ");
                }
            }
            test.Write("\n");
        }

        test.Write("\n");
        test.Write("Legend: Each cell shows attribute + column attribute combined\n");

        TestCaptureHelper.Capture(test.Terminal, "full-matrix");
    }

    /// <summary>
    /// Tests that attribute reset codes work correctly.
    /// </summary>
    [Fact]
    public void AttributeReset_WorksCorrectly()
    {
        using var test = new TestTerminal(60, 15);

        // Write test pattern: progressive attribute removal
        // Use unique chars at each stage: A=all, E=no-strike, I=no-under, M=no-italic, Q=none
        test.Write("\x1b[1;3;4;9mA\x1b[29mE\x1b[24mI\x1b[23mM\x1b[22mQ\x1b[0m\n");
        test.Write("\n");

        // Display version for SVG
        test.Write("Attribute Reset Sequences\n");
        test.Write("=========================\n\n");

        // Set multiple, then reset all
        test.Write("\x1b[1;3;4mBold+Italic+Under\x1b[0m then Normal\n");

        // Set multiple, then reset individually
        test.Write("\x1b[1;3;4mAll\x1b[24m-Under\x1b[23m-Italic\x1b[22m-Bold\x1b[0m\n");

        // Progressive attribute removal
        test.Write("\n");
        test.Write("Progressive removal:\n");
        test.Write("\x1b[1;3;4;9mFull\x1b[0m ");
        test.Write("\x1b[1;3;4;9mABCD\x1b[29mEFGH\x1b[24mIJKL\x1b[23mMNOP\x1b[22mQRST\x1b[0m\n");

        // Verify the progressive removal using first row's unique chars
        var snapshot = test.Terminal.CreateSnapshot();

        // "A" should have all four attributes
        Assert.True(HasCellWithAttributes(snapshot, 'A', 
            CellAttributes.Bold | CellAttributes.Italic | CellAttributes.Underline | CellAttributes.Strikethrough),
            "A should have all attributes");

        // "E" should have bold, italic, underline (no strikethrough)
        Assert.True(HasCellWithAttributes(snapshot, 'E', 
            CellAttributes.Bold | CellAttributes.Italic | CellAttributes.Underline),
            "E should not have strikethrough");

        // "I" should have bold, italic (no underline, no strikethrough)
        Assert.True(HasCellWithAttributes(snapshot, 'I', 
            CellAttributes.Bold | CellAttributes.Italic),
            "I should only have bold+italic");

        // "M" should have bold only
        Assert.True(HasCellWithAttributes(snapshot, 'M', CellAttributes.Bold),
            "M should only have bold");

        // "Q" should be normal
        Assert.True(HasCellWithAttributes(snapshot, 'Q', CellAttributes.None),
            "Q should be normal");

        TestCaptureHelper.Capture(test.Terminal, "attribute-reset");
    }

    /// <summary>
    /// Tests all possible combinations of the 4 most common attributes.
    /// </summary>
    [Fact]
    public void CommonAttributePowerSet_RendersCorrectly()
    {
        using var test = new TestTerminal(60, 22);

        test.Write("All Combinations of Bold, Italic, Underline, Strikethrough\n");
        test.Write("==========================================================\n\n");

        // Generate all 16 combinations (2^4)
        var attrs = new (string name, int code)[]
        {
            ("B", 1),   // Bold
            ("I", 3),   // Italic
            ("U", 4),   // Underline
            ("S", 9),   // Strikethrough
        };

        for (int mask = 0; mask < 16; mask++)
        {
            var label = new System.Text.StringBuilder();
            var codes = new List<int>();

            for (int i = 0; i < 4; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    label.Append(attrs[i].name);
                    codes.Add(attrs[i].code);
                }
                else
                {
                    label.Append('-');
                }
            }

            if (codes.Count == 0)
            {
                test.Write($"{label}  \x1b[0mSample Text\x1b[0m\n");
            }
            else
            {
                var codeStr = string.Join(";", codes);
                test.Write($"{label}  \x1b[{codeStr}mSample Text\x1b[0m\n");
            }
        }

        TestCaptureHelper.Capture(test.Terminal, "common-power-set");
    }
}
