using Hex1b;
using Hex1b.Documents;
using Hex1b.Nodes;
using Hex1b.Widgets;

// State
var selectedText = "(No selection yet)";
var copyModeActive = false;
var document = new Hex1bDocument(selectedText);
var editorState = new EditorState(document) { IsReadOnly = true };

using var cts = new CancellationTokenSource();
Hex1bApp? app = null;

// Determine shell
string shellName;
if (OperatingSystem.IsWindows())
    shellName = "pwsh";
else
    shellName = "bash";

// Create terminal with PTY
var builder = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithScrollback(1000);

if (OperatingSystem.IsWindows())
{
    builder = builder.WithPtyProcess(options =>
    {
        options.FileName = shellName;
        options.Arguments = ["-NoProfile", "-NoLogo"];
        options.WindowsPtyMode = WindowsPtyMode.RequireProxy;
    });
}
else
{
    builder = builder.WithPtyProcess(shellName, "--norc");
}

var terminal = builder
    .WithTerminalWidget(out var handle)
    .Build();

// Subscribe to text copied event — update the editor with selected text
handle.TextCopied += text =>
{
    selectedText = text;
    var deleteRange = new DocumentRange(new DocumentOffset(0), new DocumentOffset(document.Length));
    document.Apply(new ReplaceOperation(deleteRange, text));
    editorState.ClampAllCursors();
    app?.Invalidate();
};

// Subscribe to copy mode state changes — update the infobar
handle.CopyModeChanged += active =>
{
    copyModeActive = active;
    app?.Invalidate();
};

// Start terminal in background
_ = Task.Run(async () =>
{
    try { await terminal.RunAsync(cts.Token); }
    catch (OperationCanceledException) { }
});

// Build widget tree
Hex1bWidget Build(RootContext ctx)
{
    var statusItems = copyModeActive
        ? new[]
        {
            "Mode", "COPY MODE",
            "h/j/k/l", "Move",
            "v/V/^V", "Select",
            "y/Enter", "Copy",
            "Esc", "Cancel"
        }
        : new[]
        {
            "Shift+Space", "Enter Copy Mode",
            "Shift+↑/↓", "Scroll",
            "", handle.WindowTitle
        };

    return ctx.VStack(v =>
    [
        v.HSplitter(
            v.Border(
                v.Terminal(handle).Fill()
            ).Title(copyModeActive ? "Terminal [COPY MODE]" : "Terminal"),
            v.Border(
                v.Editor(editorState).Fill()
            ).Title("Selected Text"),
            leftWidth: 50
        ).Fill(),
        v.InfoBar(statusItems)
    ]);
}

// Create the display terminal
await using var displayTerminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((a, options) =>
    {
        app = a;
        return ctx => Build(ctx);
    })
    .Build();

try
{
    await displayTerminal.RunAsync(cts.Token);
}
finally
{
    cts.Cancel();
    await terminal.DisposeAsync();
}
