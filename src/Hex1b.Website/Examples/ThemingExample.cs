using Hex1b;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// An example that demonstrates Hex1b theming by showing a theme selector on the left
/// and widget examples on the right that update dynamically as themes are selected.
/// </summary>
public class ThemingExample(ILogger<ThemingExample> logger) : Hex1bExample
{
    private readonly ILogger<ThemingExample> _logger = logger;

    public override string Id => "theming";
    public override string Title => "Theming";
    public override string Description => "Dynamic theme switching with live widget preview.";

    /// <summary>
    /// State for the theming example.
    /// </summary>
    private class ThemingState
    {
        public required Hex1bTheme[] Themes { get; init; }
        public int SelectedThemeIndex { get; set; }
        public IReadOnlyList<string> ThemeItems { get; set; } = [];
        public string SampleTextBox { get; set; } = "Sample text";
        public bool ButtonClicked { get; set; }
        
        // Cached enhanced theme to avoid creating new instances on every render
        private int _cachedThemeIndex = -1;
        private Hex1bTheme? _cachedEnhancedTheme;
        
        public Hex1bTheme GetEnhancedTheme()
        {
            // Only create a new enhanced theme when the selection changes
            if (_cachedThemeIndex != SelectedThemeIndex || _cachedEnhancedTheme is null)
            {
                _cachedThemeIndex = SelectedThemeIndex;
                _cachedEnhancedTheme = CreateThemeWithPanelBackgrounds(Themes[SelectedThemeIndex]);
            }
            return _cachedEnhancedTheme;
        }
    }

    // Thread-local session state for each websocket connection
    [ThreadStatic]
    private static ThemingState? _currentSession;

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        var themes = new Hex1bTheme[]
        {
            Hex1bThemes.Default,
            Hex1bThemes.Ocean,
            Hex1bThemes.Sunset,
            Hex1bThemes.HighContrast,
            CreateForestTheme(),
            CreateNeonTheme(),
        };

        var state = new ThemingState
        {
            Themes = themes,
            ThemeItems = themes.Select(t => t.Name).ToList()
        };
        
        _currentSession = state;

        return () =>
        {
            var ctx = new RootContext();
            var currentTheme = state.Themes[state.SelectedThemeIndex];
            
            var infoBar = ctx.InfoBar([
                new InfoBarSection($" Theme: {currentTheme.Name} "),
                new InfoBarSection(" | "),
                new InfoBarSection(" ↑↓: Change  Tab: Focus  Enter: Click ")
            ]);
            
            var widget = ctx.VStack(root => [
                root.Splitter(
                    root.Panel(leftPanel => [
                        leftPanel.VStack(left => [
                            left.Text("═══ Themes ═══"),
                            left.Text(""),
                            left.List(state.ThemeItems).OnSelectionChanged(e => state.SelectedThemeIndex = e.SelectedIndex)
                        ])
                    ]),
                    root.Layout(
                        root.Panel(rightPanel => [
                            rightPanel.VStack(right => [
                                right.Text("═══ Widget Preview ═══"),
                                right.Text(""),
                                right.Border(b => [
                                    b.Text("  Content inside border"),
                                    b.Text("  with multiple lines")
                                ], title: "Border"),
                                right.Text(""),
                                right.Panel(p => [
                                    p.Text("  Panel with styled background"),
                                    p.Text("  (theme-dependent colors)")
                                ]),
                                right.Text(""),
                                right.Text("TextBox (Tab to focus):"),
                                right.TextBox(state.SampleTextBox).OnTextChanged(args => state.SampleTextBox = args.NewText),
                                right.Text(""),
                                right.Text("Button:"),
                                right.Button(
                                    state.ButtonClicked ? "Clicked!" : "Click Me")
                                    .OnClick(_ => state.ButtonClicked = !state.ButtonClicked),
                                right.Text(""),
                                right.Text("Toggle Switch (←/→ to change):"),
                                right.ToggleSwitch(["Manual", "Auto", "Delayed"]),
                                right.Text(""),
                                right.Text("InfoBar (shown at bottom):"),
                                right.Text("  Displays theme name & hints")
                            ])
                        ]),
                        ClipMode.Clip
                    ),
                    leftWidth: 20
                ).FillHeight(),
                infoBar
            ]);

            return widget;
        };
    }

    public override Func<Hex1bTheme>? CreateThemeProvider()
    {
        var session = _currentSession!;
        return () => session.GetEnhancedTheme();
    }

    /// <summary>
    /// Creates a theme that wraps the selected theme and adds complementary panel background colors.
    /// </summary>
    private static Hex1bTheme CreateThemeWithPanelBackgrounds(Hex1bTheme baseTheme)
    {
        // Get accent color from the base theme (use selected background as the primary accent)
        var accentColor = baseTheme.Get(ListTheme.SelectedBackgroundColor);
        
        // Create a darker, desaturated version of the accent for the panel background
        var panelBg = CreateComplementaryBackground(accentColor);
        
        // Clone the base theme and set our panel background
        return baseTheme.Clone($"{baseTheme.Name} (Enhanced)")
            .Set(PanelTheme.BackgroundColor, panelBg);
    }

    /// <summary>
    /// Creates a darker, more subdued background color that complements the given accent color.
    /// </summary>
    private static Hex1bColor CreateComplementaryBackground(Hex1bColor accentColor)
    {
        if (accentColor.IsDefault)
        {
            // For default color, use a subtle dark gray
            return Hex1bColor.FromRgb(20, 20, 25);
        }

        // Create a very dark version of the accent color (around 10-15% brightness)
        // This keeps the hue but makes it suitable as a background
        var r = (byte)(accentColor.R * 0.12);
        var g = (byte)(accentColor.G * 0.12);
        var b = (byte)(accentColor.B * 0.12);
        
        // Ensure minimum brightness so it's not pure black
        r = Math.Max(r, (byte)8);
        g = Math.Max(g, (byte)8);
        b = Math.Max(b, (byte)8);
        
        return Hex1bColor.FromRgb(r, g, b);
    }

    private static Hex1bTheme CreateForestTheme()
    {
        return new Hex1bTheme("Forest")
            // Buttons
            .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.White)
            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(34, 139, 34))
            // TextBox
            .Set(TextBoxTheme.CursorForegroundColor, Hex1bColor.Black)
            .Set(TextBoxTheme.CursorBackgroundColor, Hex1bColor.FromRgb(144, 238, 144))
            .Set(TextBoxTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(0, 100, 0))
            .Set(TextBoxTheme.SelectionForegroundColor, Hex1bColor.White)
            // List
            .Set(ListTheme.SelectedForegroundColor, Hex1bColor.White)
            .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.FromRgb(34, 139, 34))
            .Set(ListTheme.SelectedIndicator, "♣ ")
            // Splitter
            .Set(SplitterTheme.DividerColor, Hex1bColor.FromRgb(34, 139, 34))
            .Set(SplitterTheme.DividerCharacter, "┃")
            // Border
            .Set(BorderTheme.BorderColor, Hex1bColor.FromRgb(34, 139, 34))
            .Set(BorderTheme.TitleColor, Hex1bColor.FromRgb(144, 238, 144))
            // Panel
            .Set(PanelTheme.BackgroundColor, Hex1bColor.FromRgb(0, 50, 0))
            .Set(PanelTheme.ForegroundColor, Hex1bColor.FromRgb(144, 238, 144))
            // ToggleSwitch
            .Set(ToggleSwitchTheme.FocusedSelectedForegroundColor, Hex1bColor.White)
            .Set(ToggleSwitchTheme.FocusedSelectedBackgroundColor, Hex1bColor.FromRgb(34, 139, 34))
            .Set(ToggleSwitchTheme.UnfocusedSelectedForegroundColor, Hex1bColor.FromRgb(144, 238, 144))
            .Set(ToggleSwitchTheme.UnfocusedSelectedBackgroundColor, Hex1bColor.FromRgb(17, 70, 17))
            .Set(ToggleSwitchTheme.FocusedBracketForegroundColor, Hex1bColor.FromRgb(144, 238, 144))
            .Set(ToggleSwitchTheme.LeftBracket, "♣ ")
            .Set(ToggleSwitchTheme.RightBracket, " ♣");
    }

    private static Hex1bTheme CreateNeonTheme()
    {
        return new Hex1bTheme("Neon")
            // Buttons
            .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.Black)
            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(255, 0, 255))
            .Set(ButtonTheme.LeftBracket, "< ")
            .Set(ButtonTheme.RightBracket, " >")
            // TextBox
            .Set(TextBoxTheme.CursorForegroundColor, Hex1bColor.Black)
            .Set(TextBoxTheme.CursorBackgroundColor, Hex1bColor.FromRgb(0, 255, 255))
            .Set(TextBoxTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(255, 0, 255))
            .Set(TextBoxTheme.SelectionForegroundColor, Hex1bColor.White)
            .Set(TextBoxTheme.LeftBracket, "<")
            .Set(TextBoxTheme.RightBracket, ">")
            // List
            .Set(ListTheme.SelectedForegroundColor, Hex1bColor.Black)
            .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.FromRgb(0, 255, 255))
            .Set(ListTheme.SelectedIndicator, "* ")
            // Splitter
            .Set(SplitterTheme.DividerColor, Hex1bColor.FromRgb(255, 0, 255))
            .Set(SplitterTheme.DividerCharacter, "║")
            // Border
            .Set(BorderTheme.BorderColor, Hex1bColor.FromRgb(255, 0, 255))
            .Set(BorderTheme.TitleColor, Hex1bColor.FromRgb(0, 255, 255))
            .Set(BorderTheme.TopLeftCorner, "╔")
            .Set(BorderTheme.TopRightCorner, "╗")
            .Set(BorderTheme.BottomLeftCorner, "╚")
            .Set(BorderTheme.BottomRightCorner, "╝")
            .Set(BorderTheme.HorizontalLine, "═")
            .Set(BorderTheme.VerticalLine, "║")
            // Panel
            .Set(PanelTheme.BackgroundColor, Hex1bColor.FromRgb(30, 0, 30))
            .Set(PanelTheme.ForegroundColor, Hex1bColor.FromRgb(0, 255, 255))
            // ToggleSwitch
            .Set(ToggleSwitchTheme.FocusedSelectedForegroundColor, Hex1bColor.Black)
            .Set(ToggleSwitchTheme.FocusedSelectedBackgroundColor, Hex1bColor.FromRgb(0, 255, 255))
            .Set(ToggleSwitchTheme.UnfocusedSelectedForegroundColor, Hex1bColor.FromRgb(0, 255, 255))
            .Set(ToggleSwitchTheme.UnfocusedSelectedBackgroundColor, Hex1bColor.FromRgb(0, 80, 80))
            .Set(ToggleSwitchTheme.FocusedBracketForegroundColor, Hex1bColor.FromRgb(255, 0, 255))
            .Set(ToggleSwitchTheme.LeftBracket, "<< ")
            .Set(ToggleSwitchTheme.RightBracket, " >>")
            .Set(ToggleSwitchTheme.Separator, " ║ ");
    }
}
