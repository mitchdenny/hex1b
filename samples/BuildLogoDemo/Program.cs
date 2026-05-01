using Hex1b;
using Hex1b.Layout;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.Center(c =>
            c.Border(
                c.Surface(s => BuildLogoDemo.BuildLogo.BuildLayers(s))
                    .Width(SizeHint.Fixed(BuildLogoDemo.BuildLogo.WidthCells))
                    .Height(SizeHint.Fixed(BuildLogoDemo.BuildLogo.HeightCells))
            ).Title("BUILD")
        );
    })
    .Build();

await terminal.RunAsync();
