using Hex1b;
using Hex1b.Documents;

var document = new Hex1bDocument("# Hello\n\nInitial content.");
var editorState = new EditorState(document);

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
        ctx.HSplitter(
            ctx.Editor(editorState),
            ctx.VScrollPanel(ctx.Markdown(document)),
            leftWidth: 50
        ))
    .Build();

await terminal.RunAsync();