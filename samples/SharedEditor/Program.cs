using Hex1b;
using Hex1b.Documents;
using Hex1b.Widgets;

// Two editors sharing the same document — type in one, see it in both
var doc = new Hex1bDocument("Hello world\nThis is a shared document.\nEdit in either panel!");
var leftState = new EditorState(doc);
var rightState = new EditorState(doc) { IsReadOnly = true };

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithDiagnostics()
    .WithMouse()
    .WithHex1bApp((app, options) => ctx => ctx.HStack(h =>
    [
        h.Editor(leftState).FillWidth().FillHeight(),
        h.Editor(rightState).FillWidth().FillHeight(),
    ]))
    .Build();

await terminal.RunAsync();
