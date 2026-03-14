using Hex1b;

var currentMonth = DateOnly.FromDateTime(DateTime.Today);
var selectedDate = "None";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.VStack(v => [
            v.Text($"  {currentMonth:MMMM yyyy}"),
            v.Calendar(currentMonth)
                .Compact()
                .OnSelected(e =>
                {
                    selectedDate = e.SelectedDate.ToString("yyyy-MM-dd");
                }),
            v.Text(""),
            v.Text($"  Selected: {selectedDate}"),
            v.Text("  Arrow keys to navigate, Enter to select"),
        ]);
    })
    .Build();

await terminal.RunAsync();
