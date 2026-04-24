using Hex1b;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Widgets;

// State
var document = new Hex1bDocument("(No selection yet)");
var editorState = new EditorState(document) { IsReadOnly = true };

using var cts = new CancellationTokenSource();
Hex1bApp? app = null;

// Determine shell
string shellName = OperatingSystem.IsWindows() ? "pwsh" : "bash";

// Create terminal with PTY
var builder = Hex1b.Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithScrollback(1000);

if (OperatingSystem.IsWindows())
{
    builder = builder.WithPtyProcess(options =>
    {
        options.FileName = shellName;
        options.Arguments = ["-NoLogo"];
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

// Update editor when text is copied (clipboard is handled automatically by CopyModeBindings)
handle.TextCopied += text =>
{
    var deleteRange = new DocumentRange(new DocumentOffset(0), new DocumentOffset(document.Length));
    document.Apply(new ReplaceOperation(deleteRange, text));
    editorState.ClampAllCursors();
    app?.Invalidate();
};

// Start terminal in background — shut down app when process exits
_ = Task.Run(async () =>
{
    try { await terminal.RunAsync(cts.Token); }
    catch (OperationCanceledException) { }
    app?.RequestStop();
});

// Build widget tree
Hex1bWidget Build(RootContext ctx)
{
    var title = handle.CopyModeState switch
    {
        CopyModeState.CharacterSelection => "Terminal [CHAR SELECT]",
        CopyModeState.LineSelection => "Terminal [LINE SELECT]",
        CopyModeState.BlockSelection => "Terminal [BLOCK SELECT]",
        CopyModeState.Active => "Terminal [COPY MODE]",
        _ => "Terminal"
    };

    var statusItems = handle.CopyModeState != CopyModeState.Inactive
        ? new[]
        {
            "h/j/k/l", "Move",
            "v/V/Alt+V", "Char/Line/Block",
            "y/Enter", "Copy",
            "Esc", "Cancel"
        }
        : new[]
        {
            "F6", "Copy Mode",
            "Drag", "Select",
            "", handle.WindowTitle
        };

    return ctx.VStack(v =>
    [
        v.HSplitter(
            v.Border(
                v.Terminal(handle).CopyModeBindings().Fill()
            ).Title(title),
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
