using Hex1b;
using Hex1b.Layout;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.Surface(s => BuildLogoDemo.BuildLogo.BuildLayers(s))
            .RedrawAfter(50);
    })
    .Build();

await terminal.RunAsync();
