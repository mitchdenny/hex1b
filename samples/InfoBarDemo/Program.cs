using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

// Demo showcasing InfoBar patterns with the builder API

// State for the demo
var currentMode = "NORMAL";
var fileName = "Program.cs";
var encoding = "UTF-8";
var lineNumber = 42;
var columnNumber = 15;

// Pattern names for navigation
var patterns = new[]
{
    "1. Default Separator",
    "2. Spacer (Push Right)",
    "3. Width Control",
    "4. Spinner Integration",
    "5. Powerline Style",
    "6. Per-Section Theming"
};

var selectedPattern = 0;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.VStack(v => [
            // Header
            v.Text("InfoBar Demo"),
            v.Text("Builder API patterns"),
            v.Separator(),
            v.Text(""),

            // Pattern selector
            v.HStack(h => [
                h.Text("Pattern: "),
                h.Picker(patterns)
                    .OnSelectionChanged(e => selectedPattern = e.SelectedIndex)
            ]),
            v.Text(""),
            v.Separator(),
            v.Text(""),

            // Display description for current pattern
            v.Text(GetPatternDescription(selectedPattern)),
            v.Text(""),

            // Main content area (fills available space)
            v.Border(b => [
                b.Text("Main content area"),
                b.Text(""),
                b.Text("The InfoBar patterns below demonstrate the builder API."),
                b.Text("Use ↑↓ to select different patterns.")
            ]).Title("Content").Fill(),

            // Render the selected InfoBar pattern
            RenderInfoBarPattern(v, selectedPattern, currentMode, fileName, encoding, lineNumber, columnNumber)
        ]);
    })
    .WithMouse()
    .Build();

await terminal.RunAsync();

// Returns description text for each pattern
static string GetPatternDescription(int pattern) => pattern switch
{
    0 => "Auto-inserted separators with .WithDefaultSeparator()",
    1 => "Spacer() pushes content to right edge",
    2 => "Width control with FixedWidth() and FillWidth()",
    3 => "Widget sections with Spinner integration",
    4 => "Powerline style with colored segments",
    5 => "Per-section theming with .Theme()",
    _ => "Unknown pattern"
};

// Renders the appropriate InfoBar pattern
static Hex1bWidget RenderInfoBarPattern(
    WidgetContext<VStackWidget> v,
    int pattern,
    string mode,
    string fileName,
    string encoding,
    int line,
    int col)
{
    return pattern switch
    {
        0 => Pattern_DefaultSeparator(v, mode, fileName, line, col),
        1 => Pattern_Spacer(v, mode, fileName, line, col),
        2 => Pattern_WidthControl(v, mode, fileName, encoding, line, col),
        3 => Pattern_SpinnerIntegration(v, mode, fileName),
        4 => Pattern_Powerline(v, mode, fileName, line, col),
        5 => Pattern_SectionTheming(v, mode, fileName, line, col),
        _ => v.InfoBar("Unknown pattern")
    };
}

// Pattern 1: Default separator auto-insertion
static InfoBarWidget Pattern_DefaultSeparator(
    WidgetContext<VStackWidget> v,
    string mode,
    string fileName,
    int line,
    int col)
{
    // Separators are auto-inserted between sections
    return v.InfoBar(s => [
        s.Section(mode),
        s.Section(fileName),
        s.Section($"Ln {line}, Col {col}")
    ]).WithDefaultSeparator(" | ");
}

// Pattern 2: Spacer pushes content to the right
static InfoBarWidget Pattern_Spacer(
    WidgetContext<VStackWidget> v,
    string mode,
    string fileName,
    int line,
    int col)
{
    // Spacer() expands to fill available space
    return v.InfoBar(s => [
        s.Section(mode),
        s.Separator(" | "),
        s.Section(fileName),
        s.Spacer(),
        s.Section($"Ln {line}, Col {col}")
    ]);
}

// Pattern 3: Width control with FixedWidth and FillWidth
static InfoBarWidget Pattern_WidthControl(
    WidgetContext<VStackWidget> v,
    string mode,
    string fileName,
    string encoding,
    int line,
    int col)
{
    return v.InfoBar(s => [
        s.Section(mode).FixedWidth(10).AlignCenter(),
        s.Section(fileName).FillWidth(),
        s.Section(encoding),
        s.Section($"Ln {line}, Col {col}")
    ]).WithDefaultSeparator(" │ ");
}

// Pattern 4: Spinner integration via widget sections
static InfoBarWidget Pattern_SpinnerIntegration(
    WidgetContext<VStackWidget> v,
    string mode,
    string fileName)
{
    // Widget sections can contain any widget
    return v.InfoBar(s => [
        s.Section(mode),
        s.Section(fileName),
        s.Spacer(),
        s.Section(inner => inner.HStack(h => [
            h.Spinner(SpinnerStyle.Dots),
            h.Text(" Building...")
        ]))
    ]).WithDefaultSeparator(" | ");
}

// Pattern 5: Powerline style with colored segments
static InfoBarWidget Pattern_Powerline(
    WidgetContext<VStackWidget> v,
    string mode,
    string fileName,
    int line,
    int col)
{
    const string Sep = "◤"; // U+25E4
    
    return v.InfoBar(s => [
        s.Section($" {mode} ").Theme(t => t
            .Set(GlobalTheme.ForegroundColor, Hex1bColor.Black)
            .Set(GlobalTheme.BackgroundColor, Hex1bColor.Blue)),
        s.Separator(Sep).Theme(t => t
            .Set(GlobalTheme.ForegroundColor, Hex1bColor.Blue)
            .Set(GlobalTheme.BackgroundColor, Hex1bColor.Gray)),
        s.Section($" {fileName} ").Theme(t => t
            .Set(GlobalTheme.ForegroundColor, Hex1bColor.White)
            .Set(GlobalTheme.BackgroundColor, Hex1bColor.Gray)),
        s.Separator(Sep).Theme(t => t
            .Set(GlobalTheme.ForegroundColor, Hex1bColor.Gray)
            .Set(GlobalTheme.BackgroundColor, Hex1bColor.DarkGray)),
        s.Section($" Ln {line}, Col {col} ").Theme(t => t
            .Set(GlobalTheme.ForegroundColor, Hex1bColor.White)
            .Set(GlobalTheme.BackgroundColor, Hex1bColor.DarkGray))
    ], invertColors: false);
}

// Pattern 6: Per-section theming
static InfoBarWidget Pattern_SectionTheming(
    WidgetContext<VStackWidget> v,
    string mode,
    string fileName,
    int line,
    int col)
{
    var modeColor = mode switch
    {
        "INSERT" => Hex1bColor.Green,
        "VISUAL" => Hex1bColor.Magenta,
        _ => Hex1bColor.Blue
    };

    return v.InfoBar(s => [
        s.Section(mode).Theme(t => t.Set(GlobalTheme.ForegroundColor, modeColor)),
        s.Section(fileName),
        s.Spacer(),
        s.Section($"Ln {line}").Theme(t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan)),
        s.Separator(":"),
        s.Section($"Col {col}").Theme(t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Yellow))
    ]).WithDefaultSeparator(" | ");
}
