using Hex1b;
using Hex1b.Widgets;

// ── A filtered selection-prompt demo ──────────────────────────────
// The textbox at the top filters the list as you type. Matched characters
// in each row are highlighted in yellow. Use Up/Down to navigate the
// filtered results; Right Arrow accepts the inline prediction; Enter picks.

var commands = new[]
{
    new Command("file.open",            "Open a file from disk",                "Ctrl+O"),
    new Command("file.save",            "Save the current file",                "Ctrl+S"),
    new Command("file.save-as",         "Save the current file with a new name","Ctrl+Shift+S"),
    new Command("file.close",           "Close the current editor tab",         "Ctrl+W"),
    new Command("edit.cut",             "Cut the current selection",            "Ctrl+X"),
    new Command("edit.copy",            "Copy the current selection",           "Ctrl+C"),
    new Command("edit.paste",           "Paste from the clipboard",             "Ctrl+V"),
    new Command("edit.find",            "Find in the current file",             "Ctrl+F"),
    new Command("edit.find-in-files",   "Find across all files",                "Ctrl+Shift+F"),
    new Command("edit.replace",         "Find and replace in the current file", "Ctrl+H"),
    new Command("view.toggle-sidebar",  "Show or hide the sidebar",             "Ctrl+B"),
    new Command("view.toggle-terminal", "Show or hide the integrated terminal", "Ctrl+`"),
    new Command("view.zoom-in",         "Increase editor font size",            "Ctrl+="),
    new Command("view.zoom-out",        "Decrease editor font size",            "Ctrl+-"),
    new Command("go.to-line",           "Jump to a specific line number",       "Ctrl+G"),
    new Command("go.to-symbol",         "Jump to a symbol in the current file", "Ctrl+Shift+O"),
    new Command("git.commit",           "Commit staged changes",                "Ctrl+Enter"),
    new Command("git.push",             "Push commits to the remote",           ""),
    new Command("git.pull",             "Pull from the remote",                 ""),
    new Command("terminal.new",         "Open a new terminal",                  "Ctrl+Shift+`"),
    new Command("debug.start",          "Start debugging the project",          "F5"),
    new Command("debug.stop",           "Stop the debug session",               "Shift+F5"),
    new Command("debug.step-over",      "Step over the current statement",      "F10"),
    new Command("debug.step-into",      "Step into the current statement",      "F11"),
    new Command("workspace.reload",     "Reload the entire workspace",          ""),
};

Command? picked = null;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp(ctx => ctx.Border(b => [
        b.VStack(v => [
            v.Text(""),
            v.Text("  Command palette — start typing, ↑/↓ to navigate, → to autocomplete, Enter to pick"),
            v.Text(""),
            v.SelectionPrompt(commands)
                .ItemText(c => $"{c.Name,-22}  {c.Description}")
                .FilterText(c => c.Name)
                .Prompt("> ")
                .MaxVisibleItems(10)
                .EmptyMessage("(no matching commands)")
                .OnSelected(c =>
                {
                    picked = c;
                    Console.Beep();  // cute audible cue when something is picked
                }),
            v.Text(""),
            v.Text(picked is null
                ? "  No command picked yet."
                : $"  Last picked: {picked.Name}  ({picked.Shortcut})"),
            v.Text(""),
            v.InfoBar([
                "Ctrl+C", "Exit",
            ]),
        ])
    ]).Title(" Hex1b Command Palette "))
    .Build();

await terminal.RunAsync();

internal sealed record Command(string Name, string Description, string Shortcut);
