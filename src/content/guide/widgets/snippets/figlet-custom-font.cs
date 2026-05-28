// Load a .flf file from disk at startup.
var customFont = await FigletFont.LoadFileAsync("fonts/colossal.flf");

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(ctx =>
        ctx.FigletText("Hi!").Font(customFont))
    .Build();

await terminal.RunAsync();
