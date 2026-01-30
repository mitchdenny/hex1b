using System.Text;
using System.Text.RegularExpressions;
using Hex1b.Surfaces;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Manages loading, saving, and comparing baseline files for visual regression testing.
/// </summary>
public static partial class BaselineManager
{
    private static readonly string BaselinesPath = Path.Combine(
        AppContext.BaseDirectory, 
        "..", "..", "..", 
        "Baselines", "Table");
    
    /// <summary>
    /// Converts a surface to an ANSI string (with escape codes).
    /// </summary>
    public static string SurfaceToAnsi(Surface surface)
    {
        var fullDiff = SurfaceComparer.CreateFullDiff(surface);
        return SurfaceComparer.ToAnsiString(fullDiff);
    }
    
    /// <summary>
    /// Converts a surface to plain text (no escape codes, structure only).
    /// </summary>
    public static string SurfaceToText(Surface surface)
    {
        var sb = new StringBuilder();
        
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                var cell = surface[x, y];
                
                // Skip continuation cells (part of wide characters)
                if (cell.IsContinuation)
                    continue;
                
                // Use the character, or space if empty/null
                var ch = cell.Character;
                if (string.IsNullOrEmpty(ch) || ch == "\uE000") // \uE000 is the unwritten marker
                    sb.Append(' ');
                else
                    sb.Append(ch);
            }
            
            // Trim trailing spaces for cleaner baselines
            while (sb.Length > 0 && sb[^1] == ' ')
                sb.Length--;
            
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Strips ANSI escape codes from a string.
    /// </summary>
    public static string StripAnsi(string input)
    {
        return AnsiEscapeRegex().Replace(input, string.Empty);
    }
    
    /// <summary>
    /// Loads a baseline file. Returns null if not found.
    /// </summary>
    public static string? LoadBaseline(string baselineName, bool ansi = true)
    {
        var extension = ansi ? ".ansi" : ".txt";
        var path = Path.Combine(BaselinesPath, baselineName + extension);
        
        if (!File.Exists(path))
            return null;
        
        return File.ReadAllText(path);
    }
    
    /// <summary>
    /// Saves a baseline file.
    /// </summary>
    public static void SaveBaseline(string baselineName, string content, bool ansi = true)
    {
        var extension = ansi ? ".ansi" : ".txt";
        var path = Path.Combine(BaselinesPath, baselineName + extension);
        
        // Ensure directory exists
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        
        File.WriteAllText(path, content);
    }
    
    /// <summary>
    /// Compares content against a baseline. Returns null if match, or a diff message if mismatch.
    /// </summary>
    public static string? CompareBaseline(string expected, string actual)
    {
        if (expected == actual)
            return null;
        
        // Find first difference
        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        
        for (var i = 0; i < Math.Max(expectedLines.Length, actualLines.Length); i++)
        {
            var expectedLine = i < expectedLines.Length ? expectedLines[i] : "<missing>";
            var actualLine = i < actualLines.Length ? actualLines[i] : "<missing>";
            
            if (expectedLine != actualLine)
            {
                return $"""
                    Baseline mismatch at line {i + 1}:
                    Expected: {EscapeForDisplay(expectedLine)}
                    Actual:   {EscapeForDisplay(actualLine)}
                    """;
            }
        }
        
        return "Content differs but couldn't identify specific line";
    }
    
    /// <summary>
    /// Escapes non-printable characters for display in test output.
    /// </summary>
    private static string EscapeForDisplay(string text)
    {
        var sb = new StringBuilder();
        foreach (var c in text)
        {
            if (c == '\x1b')
                sb.Append("\\x1b");
            else if (c == '\r')
                sb.Append("\\r");
            else if (c == '\n')
                sb.Append("\\n");
            else if (c < 32)
                sb.Append($"\\x{(int)c:X2}");
            else
                sb.Append(c);
        }
        return sb.ToString();
    }
    
    [GeneratedRegex(@"\x1b\[[0-9;]*[a-zA-Z]|\x1b\][^\x07]*\x07")]
    private static partial Regex AnsiEscapeRegex();
}
