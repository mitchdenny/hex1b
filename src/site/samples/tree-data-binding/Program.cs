using Hex1b;
using Hex1b.Widgets;

var fileSystem = new[] {
    new FileNode("src", true, [
        new FileNode("Program.cs", false, []),
        new FileNode("Utils.cs", false, [])
    ]),
    new FileNode("README.md", false, [])
};

var activatedItem = "(none)";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text($"Activated: {activatedItem}"),
        v.Text(""),
        v.Tree(
            fileSystem,
            labelSelector: f => f.Name,
            childrenSelector: f => f.Children,
            iconSelector: f => f.IsFolder ? "📁" : "📄"
        )
        .OnItemActivated(e => {
            var file = e.Item.GetData<FileNode>();
            activatedItem = file.Name;
        })
    ]))
    .Build();

await terminal.RunAsync();

record FileNode(string Name, bool IsFolder, FileNode[] Children);
