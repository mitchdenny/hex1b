using Hex1b;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

// State
var selectedText = "(No selection yet)";
var copyModeActive = false;
var selectionModeLabel = "";
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
    selectionModeLabel = "";
    app?.Invalidate();
};

// Handle copy mode key input — vi-style bindings and F6 entry
handle.CopyModeInput += inputEvent =>
{
    if (inputEvent is not Hex1bKeyEvent key) return false;
    
    // F6 enters copy mode (when not already in copy mode)
    if (!handle.IsInCopyMode)
    {
        if (key.Key == Hex1bKey.F6 && key.Modifiers == Hex1bModifiers.None)
        {
            handle.EnterCopyMode();
            return true;
        }
        return false; // not handled — let it pass to the child terminal
    }
    
    // Update selection mode label for InfoBar
    if (handle.Selection is { IsSelecting: true } sel)
    {
        selectionModeLabel = sel.Mode switch
        {
            SelectionMode.Character => " [CHAR]",
            SelectionMode.Line => " [LINE]",
            SelectionMode.Block => " [BLOCK]",
            _ => ""
        };
    }
    else
    {
        selectionModeLabel = "";
    }

    switch (key.Key)
    {
        // Cancel
        case Hex1bKey.Escape:
        case Hex1bKey.Q:
            handle.ExitCopyMode();
            return true;
        
        // Copy and exit
        case Hex1bKey.Enter:
        case Hex1bKey.Y:
            handle.CopySelection();
            return true;
        
        // Navigation — arrows and vi keys
        case Hex1bKey.UpArrow or Hex1bKey.K:
            handle.MoveCopyModeCursor(-1, 0);
            return true;
        case Hex1bKey.DownArrow or Hex1bKey.J:
            handle.MoveCopyModeCursor(1, 0);
            return true;
        case Hex1bKey.LeftArrow or Hex1bKey.H:
            handle.MoveCopyModeCursor(0, -1);
            return true;
        case Hex1bKey.RightArrow or Hex1bKey.L:
            handle.MoveCopyModeCursor(0, 1);
            return true;
        
        // Page navigation
        case Hex1bKey.PageUp:
            handle.MoveCopyModeCursor(-20, 0);
            return true;
        case Hex1bKey.PageDown:
            handle.MoveCopyModeCursor(20, 0);
            return true;
        
        // Line start/end
        case Hex1bKey.Home or Hex1bKey.D0:
            handle.SetCopyModeCursorPosition(handle.Selection!.Cursor.Row, 0);
            return true;
        case Hex1bKey.End:
            handle.SetCopyModeCursorPosition(handle.Selection!.Cursor.Row, handle.Width - 1);
            return true;
        
        // Buffer top/bottom
        case Hex1bKey.G when key.Modifiers == Hex1bModifiers.Shift:
            handle.SetCopyModeCursorPosition(handle.VirtualBufferHeight - 1, handle.Selection!.Cursor.Column);
            return true;
        case Hex1bKey.G:
            handle.SetCopyModeCursorPosition(0, handle.Selection!.Cursor.Column);
            return true;
        
        // Selection modes
        case Hex1bKey.V when key.Modifiers == Hex1bModifiers.None:
            handle.StartOrToggleSelection(SelectionMode.Character);
            return true;
        case Hex1bKey.V when key.Modifiers == Hex1bModifiers.Shift:
            handle.StartOrToggleSelection(SelectionMode.Line);
            return true;
        case Hex1bKey.V when key.Modifiers == Hex1bModifiers.Alt:
            handle.StartOrToggleSelection(SelectionMode.Block);
            return true;
        case Hex1bKey.Spacebar:
            handle.StartOrToggleSelection(SelectionMode.Character);
            return true;
        
        // Word navigation
        case Hex1bKey.W:
            handle.MoveWordForward();
            return true;
        case Hex1bKey.B:
            handle.MoveWordBackward();
            return true;
        
        default:
            return true; // consume all keys in copy mode
    }
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
    var statusItems = copyModeActive
        ? new[]
        {
            "Mode", $"COPY MODE{selectionModeLabel}",
            "h/j/k/l", "Move",
            "v/V/Alt+V", "Char/Line/Block",
            "y/Enter", "Copy",
            "Esc", "Cancel"
        }
        : new[]
        {
            "F6", "Copy Mode",
            "Shift+↑/↓", "Scroll",
            "", handle.WindowTitle
        };

    return ctx.VStack(v =>
    [
        v.HSplitter(
            v.Border(
                v.Terminal(handle).Fill()
            ).Title(copyModeActive ? $"Terminal [COPY MODE{selectionModeLabel}]" : "Terminal"),
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
