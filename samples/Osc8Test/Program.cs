using Hex1b;
using Hex1b.Terminal;
using Hex1b.Terminal.Testing;
using System.Reflection;

// Test OSC 8 hyperlink support
// This creates a terminal snapshot with hyperlinks and exports to HTML for inspection

Console.WriteLine("OSC 8 Hyperlink Test");
Console.WriteLine("====================");
Console.WriteLine();

// Create a headless terminal for testing
using var workload = new Hex1bAppWorkloadAdapter();
using var terminal = new Hex1bTerminal(workload, 80, 24);

// Test 1: Simple hyperlink
Console.WriteLine("Test 1: Simple hyperlink");
workload.Write("Visit ");
workload.Write("\x1b]8;;https://github.com/mitchdenny/hex1b\x1b\\");
workload.Write("Hex1b on GitHub");
workload.Write("\x1b]8;;\x1b\\");
workload.Write(" for more info.\n");

// Test 2: Hyperlink with ID parameter
Console.WriteLine("Test 2: Hyperlink with ID parameter");
workload.Write("Documentation: ");
workload.Write("\x1b]8;id=docs;https://hex1b.dev/docs\x1b\\");
workload.Write("hex1b.dev/docs");
workload.Write("\x1b]8;;\x1b\\");
workload.Write("\n");

// Test 3: Multiple hyperlinks on same line
Console.WriteLine("Test 3: Multiple hyperlinks");
workload.Write("Links: ");
workload.Write("\x1b]8;;https://example.com\x1b\\");
workload.Write("[Example]");
workload.Write("\x1b]8;;\x1b\\");
workload.Write(" ");
workload.Write("\x1b]8;;https://test.com\x1b\\");
workload.Write("[Test]");
workload.Write("\x1b]8;;\x1b\\");
workload.Write("\n");

// Test 4: Complex URL
Console.WriteLine("Test 4: Complex URL with query params");
workload.Write("Search: ");
workload.Write("\x1b]8;;https://www.google.com/search?q=osc+8+hyperlinks&hl=en\x1b\\");
workload.Write("Google Search Results");
workload.Write("\x1b]8;;\x1b\\");
workload.Write("\n\n");

// Test 5: Multiline hyperlink
Console.WriteLine("Test 5: Multiline hyperlink");
workload.Write("\x1b]8;;https://en.wikipedia.org/wiki/ANSI_escape_code\x1b\\");
workload.Write("This is a very long link that\nspans multiple lines in the\nterminal output.");
workload.Write("\x1b]8;;\x1b\\");
workload.Write("\n\n");

// Test 6: Same link multiple times (deduplication)
Console.WriteLine("Test 6: Link deduplication");
workload.Write("\x1b]8;;https://example.com\x1b\\");
workload.Write("First");
workload.Write("\x1b]8;;\x1b\\");
workload.Write(" and ");
workload.Write("\x1b]8;;https://example.com\x1b\\");
workload.Write("Second");
workload.Write("\x1b]8;;\x1b\\");
workload.Write(" use same URL\n");

// Use reflection to access internal methods for testing
var flushMethod = typeof(Hex1bTerminal).GetMethod("FlushOutput", BindingFlags.Instance | BindingFlags.NonPublic);
flushMethod?.Invoke(terminal, null);

var containsMethod = typeof(Hex1bTerminal).GetMethod("ContainsHyperlinkData", BindingFlags.Instance | BindingFlags.NonPublic);
var hasHyperlinks = (bool)(containsMethod?.Invoke(terminal, null) ?? false);

var snapshotMethod = typeof(Hex1bTerminal).GetMethod("TakeSnapshot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
var snapshot = snapshotMethod?.Invoke(terminal, null);

Console.WriteLine();
Console.WriteLine($"Terminal contains hyperlinks: {hasHyperlinks}");
Console.WriteLine();

if (snapshot != null)
{
    // Create HTML export
    var toHtmlMethod = snapshot.GetType().GetMethod("ToHtml");
    var html = (string)(toHtmlMethod?.Invoke(snapshot, new object?[] { null }) ?? "");

    // Save to file
    var outputPath = Path.Combine(Path.GetTempPath(), "hex1b-osc8-test.html");
    File.WriteAllText(outputPath, html);

    Console.WriteLine($"HTML export saved to: {outputPath}");
    Console.WriteLine();
    Console.WriteLine("Open the file in a browser to inspect the hyperlinks.");
    Console.WriteLine("Hover over cells to see hyperlink information in the tooltip.");
    Console.WriteLine();
}

Console.WriteLine("Test completed successfully!");
Console.WriteLine();
Console.WriteLine("Press any key to exit...");
Console.ReadKey(true);
