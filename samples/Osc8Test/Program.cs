using Hex1b;
using Hex1b.Widgets;

// Test OSC 8 hyperlink support using the HyperlinkWidget
// This demonstrates clickable terminal hyperlinks in a Hex1b TUI app

Console.WriteLine("OSC 8 Hyperlink Widget Test");
Console.WriteLine("===========================");
Console.WriteLine("Press any key to start the TUI...");
Console.ReadKey(true);

var clickedLinks = new List<string>();
var lastClickedUri = "(none)";

try
{
    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithHex1bApp((app, options) => ctx => ctx.VStack(root => [
            root.Border(
                root.VStack(content => [
                    content.Text("OSC 8 Hyperlink Widget Demo"),
                    content.Text("══════════════════════════════════════════════"),
                    content.Text(""),
                    content.Text("These are clickable hyperlinks (in supported terminals):"),
                    content.Text(""),
                    
                    // Simple hyperlink
                    content.HStack(row => [
                        row.Text("1. Simple link: "),
                        row.Hyperlink("Hex1b on GitHub", "https://github.com/mitchdenny/hex1b")
                            .OnClick(e => {
                                lastClickedUri = e.Uri;
                                clickedLinks.Add(e.Uri);
                            }),
                    ]),
                    
                    // Hyperlink to documentation
                    content.HStack(row => [
                        row.Text("2. Documentation: "),
                        row.Hyperlink("hex1b.dev", "https://hex1b.dev")
                            .OnClick(e => {
                                lastClickedUri = e.Uri;
                                clickedLinks.Add(e.Uri);
                            }),
                    ]),
                    
                    // Multiple links on one line
                    content.HStack(row => [
                        row.Text("3. Multiple links: "),
                        row.Hyperlink("[Example]", "https://example.com")
                            .OnClick(e => { lastClickedUri = e.Uri; clickedLinks.Add(e.Uri); }),
                        row.Text(" "),
                        row.Hyperlink("[Test]", "https://test.com")
                            .OnClick(e => { lastClickedUri = e.Uri; clickedLinks.Add(e.Uri); }),
                        row.Text(" "),
                        row.Hyperlink("[Info]", "https://info.com")
                            .OnClick(e => { lastClickedUri = e.Uri; clickedLinks.Add(e.Uri); }),
                    ]),
                    
                    // Complex URL
                    content.HStack(row => [
                        row.Text("4. Search link: "),
                        row.Hyperlink("Google OSC 8", "https://www.google.com/search?q=osc+8+hyperlinks")
                            .OnClick(e => { lastClickedUri = e.Uri; clickedLinks.Add(e.Uri); }),
                    ]),
                    
                    // Wikipedia
                    content.HStack(row => [
                        row.Text("5. Wikipedia: "),
                        row.Hyperlink("ANSI Escape Codes", "https://en.wikipedia.org/wiki/ANSI_escape_code")
                            .OnClick(e => { lastClickedUri = e.Uri; clickedLinks.Add(e.Uri); }),
                    ]),
                    
                    content.Text(""),
                    content.Text("══════════════════════════════════════════════"),
                    content.Text($"Last clicked: {lastClickedUri}"),
                    content.Text($"Total clicks: {clickedLinks.Count}"),
                    content.Text(""),
                    content.Text("Tip: Hover over links to see the URL in your terminal."),
                    content.Text("     Press Enter or click to activate a link."),
                    content.Text("     Use Tab/Shift+Tab to navigate between links."),
                ])
            ).Title("OSC 8 Hyperlinks").Fill(),
            
            // InfoBar at the bottom
            root.InfoBar([
                "Tab", "Next link",
                "Enter/Click", "Activate",
                "Ctrl+C", "Exit"
            ]),
        ]))
        .WithMouse()
        .Build();

    await terminal.RunAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey(true);
}

Console.WriteLine();
Console.WriteLine($"Clicked {clickedLinks.Count} links during the session:");
foreach (var uri in clickedLinks)
{
    Console.WriteLine($"  - {uri}");
}
Console.WriteLine();
Console.WriteLine("Done!");
