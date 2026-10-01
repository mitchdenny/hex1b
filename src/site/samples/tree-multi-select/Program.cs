using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Tree(t => [
            t.Item("Frontend", fe => [
                fe.Item("React"),
                fe.Item("Vue"),
                fe.Item("Angular")
            ]).Expanded(),
            t.Item("Backend", be => [
                be.Item("Node.js"),
                be.Item("Python"),
                be.Item("Go")
            ]).Expanded()
        ])
        .MultiSelect()
        .OnSelectionChanged(e => {
            var selected = e.SelectedItems.Select(i => i.Label);
            Console.WriteLine($"Selected: {string.Join(", ", selected)}");
        })
    ]))
    .Build();

await terminal.RunAsync();
