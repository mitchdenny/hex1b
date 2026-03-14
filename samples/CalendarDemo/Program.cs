using Hex1b;

var currentMonth = DateOnly.FromDateTime(DateTime.Today);
var selectedDate = "None";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.TabPanel(tp => [
            tp.Tab("Compact", t => [
                t.Text($"  {currentMonth:MMMM yyyy}"),
                t.Text(""),
                t.Calendar(currentMonth)
                    .Compact()
                    .OnSelected(e =>
                    {
                        selectedDate = e.SelectedDate.ToString("yyyy-MM-dd");
                    }),
                t.Text(""),
                t.Text($"  Selected: {selectedDate}"),
            ]).Selected(),

            tp.Tab("Bordered", t => [
                t.Text($"  {currentMonth:MMMM yyyy}"),
                t.Text(""),
                t.Calendar(currentMonth)
                    .OnSelected(e =>
                    {
                        selectedDate = e.SelectedDate.ToString("yyyy-MM-dd");
                    }),
                t.Text(""),
                t.Text($"  Selected: {selectedDate}"),
            ]),

            tp.Tab("Day Content", t => [
                t.Text($"  {currentMonth:MMMM yyyy}"),
                t.Text(""),
                t.Calendar(currentMonth)
                    .Day(day =>
                    {
                        if (day.Date.Day == 25)
                            return new Hex1b.Widgets.TextBlockWidget(" 🎂");
                        if (day.DayOfWeek == DayOfWeek.Friday)
                            return new Hex1b.Widgets.TextBlockWidget(" 🍕");
                        return null;
                    })
                    .OnSelected(e =>
                    {
                        selectedDate = e.SelectedDate.ToString("yyyy-MM-dd");
                    }),
                t.Text(""),
                t.Text($"  Selected: {selectedDate}"),
            ]),
        ]);
    })
    .Build();

await terminal.RunAsync();
