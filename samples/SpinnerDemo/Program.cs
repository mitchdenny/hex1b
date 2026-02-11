using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

// Available theme configurations
var themeConfigs = new (string Name, Action<Hex1bTheme> Configure)[]
{
    ("Default", _ => { }),
    ("Ocean", t => { t.Set(SpinnerTheme.Style, SpinnerStyle.DotsScrolling); t.Set(SpinnerTheme.ForegroundColor, Hex1bColor.FromRgb(100, 200, 255)); }),
    ("HighContrast", t => { t.Set(SpinnerTheme.Style, SpinnerStyle.Line); t.Set(SpinnerTheme.ForegroundColor, Hex1bColor.Yellow); }),
    ("Sunset", t => { t.Set(SpinnerTheme.Style, SpinnerStyle.Bounce); t.Set(SpinnerTheme.ForegroundColor, Hex1bColor.FromRgb(255, 140, 60)); }),
    ("ASCII Only", t => { t.Set(SpinnerTheme.Style, SpinnerStyle.Line); })
};
int selectedThemeIndex = 0;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        var currentThemeConfig = themeConfigs[selectedThemeIndex];

        return ctx.VStack(v => [
            // Header
            v.Text("Spinner Demo"),
            v.Separator(),
            v.Text(""),

            // Built-in styles section (time-based animation)
            v.Text("Built-in Styles (single character):"),
            v.HStack(h => [
                h.Spinner(SpinnerStyle.Dots), h.Text(" Dots  "),
                h.Spinner(SpinnerStyle.DotsScrolling), h.Text(" DotsScrolling  "),
                h.Spinner(SpinnerStyle.Line), h.Text(" Line  "),
                h.Spinner(SpinnerStyle.Arrow), h.Text(" Arrow  ")
            ]),
            v.HStack(h => [
                h.Spinner(SpinnerStyle.Circle), h.Text(" Circle  "),
                h.Spinner(SpinnerStyle.Square), h.Text(" Square  "),
                h.Spinner(SpinnerStyle.Bounce), h.Text(" Bounce  "),
                h.Spinner(SpinnerStyle.GrowHorizontal), h.Text(" GrowH  ")
            ]),
            v.HStack(h => [
                h.Spinner(SpinnerStyle.GrowVertical), h.Text(" GrowV")
            ]),
            v.Text(""),

            // Multi-character styles section
            v.Text("Multi-Character Styles:"),
            v.HStack(h => [
                h.Spinner(SpinnerStyle.BouncingBall), h.Text(" BouncingBall  "),
                h.Spinner(SpinnerStyle.LoadingBar), h.Text(" LoadingBar  ")
            ]),
            v.HStack(h => [
                h.Spinner(SpinnerStyle.Segments), h.Text(" Segments")
            ]),
            v.Text(""),

            v.Separator(),

            // Theme selector
            v.HStack(h => [
                h.Text("Theme: "),
                h.Picker(themeConfigs.Select(t => t.Name).ToArray())
                    .OnSelectionChanged(e => selectedThemeIndex = e.SelectedIndex)
            ]),
            v.Text(""),

            // Themed spinner panel
            v.ThemePanel(
                theme =>
                {
                    currentThemeConfig.Configure(theme);
                    return theme;
                },
                t => [
                    t.Border(b => [
                        b.VStack(inner => [
                            inner.HStack(h => [
                                h.Spinner(), // Uses theme default style, time-based
                                h.Text(" This spinner uses the theme's default style")
                            ]),
                            inner.Text($"Current theme: {currentThemeConfig.Name}"),
                            inner.Text($"Spinner style: {GetSpinnerStyleName(currentThemeConfig.Name)}")
                        ])
                    ]).Title("Themed Spinner Panel")
                ]
            ),

            v.Text(""),
            v.Separator(),
            v.Text("Press Tab to navigate, Ctrl+C to exit"),
            v.Text("(Animation is time-based - mouse movement doesn't speed it up!)")
        ]); // No .RedrawAfter() needed - spinners schedule their own redraws
    })
    .WithMouse()
    .Build();

await terminal.RunAsync();

static string GetSpinnerStyleName(string themeName) => themeName switch
{
    "Default" => "Dots (default)",
    "Ocean" => "DotsScrolling",
    "HighContrast" => "Line",
    "Sunset" => "Bounce",
    "ASCII Only" => "Line",
    _ => "Unknown"
};

