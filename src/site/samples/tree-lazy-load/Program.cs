using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Tree(t => [
        t.Item("Server 1").Icon("🖥️")
            .OnExpanding(async _ => {
                await Task.Delay(1000); // Simulate network call
                return [
                    new TreeContext().Item("Database").Icon("🗃️"),
                    new TreeContext().Item("Cache").Icon("💾")
                ];
            }),
        t.Item("Server 2").Icon("🖥️")
            .OnExpanding(async _ => {
                await Task.Delay(500);
                return [
                    new TreeContext().Item("API Gateway").Icon("🌐")
                ];
            })
    ]))
    .Build();

await terminal.RunAsync();
