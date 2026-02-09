using Hex1b;
using Hex1b.Documents;
using Hex1b.Widgets;

// Three editors sharing the same document — text editor, read-only mirror, and hex view
var doc = new Hex1bDocument("Hello world\nThis is a shared document.\nEdit in either panel!");
var leftState = new EditorState(doc);
var rightState = new EditorState(doc) { IsReadOnly = true };
var hexState = new EditorState(doc) { IsReadOnly = true };

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithDiagnostics()
    .WithMouse()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v =>
    [
        v.HStack(h =>
        [
            h.Editor(leftState).FillWidth().FillHeight(),
            h.Editor(rightState).FillWidth().FillHeight(),
        ]).FillWidth().FillHeight(),
        v.Editor(hexState)
            .WithViewRenderer(HexEditorViewRenderer.Instance)
            .FillWidth()
            .FixedHeight(8),
    ]))
    .Build();

await terminal.RunAsync();
