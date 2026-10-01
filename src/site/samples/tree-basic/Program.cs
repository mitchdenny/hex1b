using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Tree(t => [
        t.Item("Documents", docs => [
            docs.Item("Resume.pdf").Icon("📄"),
            docs.Item("Cover Letter.docx").Icon("📄")
        ]).Icon("📁").Expanded(),
        t.Item("Pictures", pics => [
            pics.Item("Vacation").Icon("📁"),
            pics.Item("Family").Icon("📁")
        ]).Icon("📸"),
        t.Item("README.md").Icon("📄")
    ]))
    .Build();

await terminal.RunAsync();
