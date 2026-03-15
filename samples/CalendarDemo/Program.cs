using Hex1b;
using Hex1b.Widgets;

var currentMonth = DateOnly.FromDateTime(DateTime.Today);
var selectedDate = "None";
var modeIndex = 0;
string[] modes = ["Compact", "Without Content", "With Content", "Date Picker"];
var pickerDate = "None";

await using var terminal = Hex1b.Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.VStack(v =>
        {
            var widgets = new List<Hex1bWidget>
            {
                v.ToggleSwitch(modes, modeIndex)
                    .OnSelectionChanged(e => modeIndex = e.SelectedIndex),
                v.Text(""),
            };

            if (modeIndex == 3)
            {
                // Date Picker mode
                var datePicker = v.DatePicker()
                    .Placeholder("Pick a date...")
                    .Format("MMMM d, yyyy")
                    .OnSelected(e =>
                    {
                        pickerDate = e.SelectedDate.ToString("yyyy-MM-dd");
                    });

                widgets.Add(datePicker);
                widgets.Add(v.Text($"  Picked: {pickerDate}"));
            }
            else
            {
                // Calendar modes
                var calendar = v.Calendar(currentMonth)
                    .HighlightCurrent()
                    .OnSelected(e =>
                    {
                        selectedDate = e.SelectedDate.ToString("yyyy-MM-dd");
                    });

                calendar = modeIndex switch
                {
                    0 => calendar.Compact(),
                    2 => calendar.Day(day =>
                    {
                        if (day.Date.Day == 25)
                            return new TextBlockWidget("Birthday");
                        if (day.DayOfWeek == DayOfWeek.Friday)
                            return new TextBlockWidget("Pizza");
                        return null;
                    }),
                    _ => calendar,
                };

                widgets.Add(v.Text($"  {currentMonth:MMMM yyyy}"));
                widgets.Add(v.Text(""));
                widgets.Add(calendar);
                widgets.Add(v.Text(""));
                widgets.Add(v.Text($"  Selected: {selectedDate}"));
            }

            return widgets.ToArray();
        });
    })
    .Build();

await terminal.RunAsync();
