using System.Globalization;
using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;

// Unicode blocks of interest for UI symbols
var unicodeBlocks = new (string Name, int Start, int End)[]
{
    ("Block Elements", 0x2580, 0x259F),
    ("Geometric Shapes", 0x25A0, 0x25FF),
    ("Box Drawing", 0x2500, 0x257F),
    ("Miscellaneous Symbols", 0x2600, 0x26FF),
    ("Dingbats", 0x2700, 0x27BF),
    ("Arrows", 0x2190, 0x21FF),
    ("Mathematical Operators", 0x2200, 0x22FF),
    ("Miscellaneous Technical", 0x2300, 0x23FF),
    ("Enclosed Alphanumerics", 0x2460, 0x24FF),
    ("Braille Patterns", 0x2800, 0x28FF),
    ("Supplemental Arrows-B", 0x2900, 0x297F),
    ("Miscellaneous Symbols and Arrows", 0x2B00, 0x2BFF),
    ("General Punctuation", 0x2000, 0x206F),
    ("Currency Symbols", 0x20A0, 0x20CF),
};

var selectedBlockIndex = 0;
var copiedChar = "";
object? focusedKey = null;

// Build character data for current block
List<UnicodeChar> GetBlockCharacters(int blockIndex)
{
    var block = unicodeBlocks[blockIndex];
    var chars = new List<UnicodeChar>();
    
    for (int cp = block.Start; cp <= block.End; cp++)
    {
        try
        {
            string glyph;
            if (cp > 0xFFFF)
            {
                glyph = char.ConvertFromUtf32(cp);
            }
            else
            {
                glyph = ((char)cp).ToString();
            }
            
            var category = CharUnicodeInfo.GetUnicodeCategory(glyph, 0);
            
            // Skip control characters and unassigned
            if (category == UnicodeCategory.Control || 
                category == UnicodeCategory.OtherNotAssigned ||
                category == UnicodeCategory.Surrogate)
                continue;
            
            var code = cp > 0xFFFF ? $"U+{cp:X5}" : $"U+{cp:X4}";
            var categoryName = GetCategoryShortName(category);
            var width = DisplayWidth.GetGraphemeWidth(glyph);
            
            chars.Add(new UnicodeChar(glyph, cp, code, categoryName, width));
        }
        catch
        {
            // Skip invalid codepoints
        }
    }
    
    return chars;
}

string GetCategoryShortName(UnicodeCategory category) => category switch
{
    UnicodeCategory.UppercaseLetter => "Lu",
    UnicodeCategory.LowercaseLetter => "Ll",
    UnicodeCategory.DecimalDigitNumber => "Nd",
    UnicodeCategory.LetterNumber => "Nl",
    UnicodeCategory.OtherNumber => "No",
    UnicodeCategory.SpaceSeparator => "Zs",
    UnicodeCategory.DashPunctuation => "Pd",
    UnicodeCategory.OpenPunctuation => "Ps",
    UnicodeCategory.ClosePunctuation => "Pe",
    UnicodeCategory.OtherPunctuation => "Po",
    UnicodeCategory.MathSymbol => "Sm",
    UnicodeCategory.CurrencySymbol => "Sc",
    UnicodeCategory.ModifierSymbol => "Sk",
    UnicodeCategory.OtherSymbol => "So",
    _ => "??"
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        var currentBlock = unicodeBlocks[selectedBlockIndex];
        var characters = GetBlockCharacters(selectedBlockIndex);

        return ctx.VStack(v => [
            v.Text("Unicode Character Selector"),
            v.Separator(),
            v.Text(""),
            
            // Block selector
            v.HStack(h => [
                h.Text("Block: ").FixedWidth(7),
                h.Picker(unicodeBlocks.Select(b => b.Name).ToArray(), selectedBlockIndex)
                    .OnSelectionChanged(e => { selectedBlockIndex = e.SelectedIndex; focusedKey = null; })
                    .FillWidth()
            ]),
            v.Text(""),
            
            // Character table
            v.Border(b => [
                b.Table(characters)
                    .RowKey(c => c.Codepoint)
                    .Header(h => [
                        TableCellExtensions.Fixed(h.Cell("Char"), 6),
                        TableCellExtensions.Fixed(h.Cell("W"), 3),
                        TableCellExtensions.Fixed(h.Cell("Code"), 10),
                        TableCellExtensions.Fixed(h.Cell("Cat"), 5),
                        TableCellExtensions.Fixed(h.Cell("Dec"), 8),
                        TableCellExtensions.Fill(h.Cell("Hex"))
                    ])
                    .Row((r, c, state) => [
                        r.Cell($" {c.Glyph} "),
                        r.Cell(c.Width.ToString()),
                        r.Cell(c.Code),
                        r.Cell(c.Category),
                        r.Cell(c.Codepoint.ToString()),
                        r.Cell($"0x{c.Codepoint:X}")
                    ])
                    .Focus(focusedKey)
                    .OnFocusChanged(key => focusedKey = key)
                    .OnRowActivated((key, row) => 
                    {
                        copiedChar = row.Glyph;
                    })
                    .Fill()
            ], title: $"{currentBlock.Name} ({characters.Count} chars)").FillHeight(),
            v.Text(""),
            
            // Status bar
            v.Text(string.IsNullOrEmpty(copiedChar) 
                ? "Press Enter to copy character to clipboard" 
                : $"Copied: '{copiedChar}' to clipboard"),
            v.Separator(),
            v.Text("↑↓: Navigate | Tab: Switch control | Enter: Copy | Ctrl+C: Exit")
        ]);
    })
    .WithMouse()
    .Build();

await terminal.RunAsync();

// Character data class - must be at end for top-level statements
class UnicodeChar(string glyph, int codepoint, string code, string category, int width)
{
    public string Glyph { get; } = glyph;
    public int Codepoint { get; } = codepoint;
    public string Code { get; } = code;
    public string Category { get; } = category;
    public int Width { get; } = width;
}
