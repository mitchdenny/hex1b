using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

// Track paste state
int pasteCount = 0;
long totalBytesReceived = 0;
string lastPastePreview = "(no paste yet)";
bool pasteInProgress = false;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.VStack(v => [
            v.Text("Bracketed Paste Demo"),
            v.Separator(),
            v.Text(""),
            v.Text("Paste something into this terminal to see bracketed paste in action."),
            v.Text("The terminal enables ESC[?2004h on startup and detects paste brackets."),
            v.Text(""),
            v.Separator(),
            v.Text($"Paste count:      {pasteCount}"),
            v.Text($"Total bytes:      {totalBytesReceived:N0}"),
            v.Text($"Paste in progress: {(pasteInProgress ? "YES" : "no")}"),
            v.Text(""),
            v.Text("Last paste preview:"),
            v.Text(lastPastePreview),
            v.Text(""),
            v.Separator(),
            v.Text("Press Ctrl+C or q to exit."),
        ]);
    })
    .Build();

await terminal.RunAsync();
