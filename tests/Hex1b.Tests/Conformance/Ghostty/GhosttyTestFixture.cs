using Hex1b.Tokens;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Shared test infrastructure for Ghostty conformance testing.
/// Creates terminals configured to behave as Hex1b would when running inside Ghostty.
/// </summary>
/// <remarks>
/// Ghostty's tests represent their interpretation of VT terminal standards.
/// Failures are triaged as: Bug, MissingFeature, IntentionalDivergence, or GhosttySpecific.
/// Use [Trait("Category", "GhosttyConformance")] on all test classes.
/// </remarks>
public static class GhosttyTestFixture
{
    /// <summary>
    /// Creates a terminal configured with Ghostty-appropriate capabilities.
    /// This mirrors what Hex1b would use when TERM_PROGRAM=ghostty.
    /// </summary>
    public static Hex1bTerminal CreateTerminal(int cols = 80, int rows = 24)
    {
        var workload = new Hex1bAppWorkloadAdapter();
        return Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(cols, rows)
            .Build();
    }

    /// <summary>
    /// Feeds raw ANSI escape sequence string to a terminal and returns it for inspection.
    /// </summary>
    public static void Feed(Hex1bTerminal terminal, string ansiSequence)
    {
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(ansiSequence));
    }

    /// <summary>
    /// Feeds raw ANSI bytes (for cases where exact byte sequences matter).
    /// </summary>
    public static void FeedBytes(Hex1bTerminal terminal, byte[] bytes)
    {
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(text));
    }

    /// <summary>
    /// Gets the trimmed text content of a terminal line (0-based).
    /// </summary>
    public static string GetLine(Hex1bTerminal terminal, int row)
    {
        using var snapshot = terminal.CreateSnapshot();
        return snapshot.GetLineTrimmed(row);
    }

    /// <summary>
    /// Gets the screen buffer cell at (row, col) for color/attribute inspection.
    /// </summary>
    public static TerminalCell GetCell(Hex1bTerminal terminal, int row, int col)
    {
        var buffer = terminal.GetScreenBuffer();
        return buffer[row, col];
    }
}
