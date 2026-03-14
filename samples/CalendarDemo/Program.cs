using Hex1b;

var currentMonth = DateOnly.FromDateTime(DateTime.Today);

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.Border(b => [
            b.Text($"  {currentMonth:MMMM yyyy}"),
            b.Text(""),
            b.Calendar(currentMonth),
        ]).Title("Calendar Demo");
    })
    .Build();

await terminal.RunAsync();
