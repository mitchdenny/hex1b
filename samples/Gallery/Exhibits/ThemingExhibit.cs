using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Gallery.Exhibits;

/// <summary>
/// An exhibit that demonstrates Hex1b theming by showing a theme selector on the left
/// and widget examples on the right that update dynamically as themes are selected.
/// </summary>
public class ThemingExhibit : Hex1bExhibit
{
    public override string Id => "theming";
    public override string Title => "Theming";
    public override string Description => "Dynamic theme switching with live widget preview.";

    public override string SourceCode => """
        // Available themes
        var themes = new[] {
            Hex1bThemes.Default,
            Hex1bThemes.Ocean,
            Hex1bThemes.Sunset,
            Hex1bThemes.HighContrast,
            CreateForestTheme(),
            CreateNeonTheme()
        };
        
        var themeList = new ListState {
            Items = themes.Select(t => 
                new ListItem(t.Name, t.Name)).ToList()
        };
        
        // Dynamic theme provider - called on each render
        Hex1bTheme GetCurrentTheme() => 
            themes[themeList.SelectedIndex];
        
        var app = new Hex1bApp(
            rootComponent: BuildUI,
            terminal: terminal,
            themeProvider: GetCurrentTheme
        );
        await app.RunAsync();
        """;

    /// <summary>
    /// Creates a new session state that is shared between widget builder and theme provider.
    /// </summary>
    private ThemingSessionState CreateSessionState()
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

        return new ThemingSessionState
        {
            Themes = themes,
            ThemeListState = new ListState
            {
                Items = themes.Select(t => new ListItem(t.Name, t.Name)).ToList(),
                SelectedIndex = 0
            },
            SampleTextBoxState = new TextBoxState { Text = "Sample text" },
            ButtonClicked = false
        };
    }

    // Thread-local session state for each websocket connection
    [ThreadStatic]
    private static ThemingSessionState? _currentSession;

    public override Func<CancellationToken, Task<Hex1bWidget>> CreateWidgetBuilder()
    {
        // Create new session state for this connection
        _currentSession = CreateSessionState();
        var session = _currentSession;

        return ct => Task.FromResult<Hex1bWidget>(
            new SplitterWidget(
                Left: new VStackWidget([
                    new TextBlockWidget("═══ Themes ═══"),
                    new TextBlockWidget(""),
                    new ListWidget(session.ThemeListState)
                ]),
                Right: new VStackWidget([
                    new TextBlockWidget("═══ Widget Preview ═══"),
                    new TextBlockWidget(""),
                    new TextBlockWidget("TextBlock:"),
                    new TextBlockWidget("  Hello, themed world!"),
                    new TextBlockWidget(""),
                    new TextBlockWidget("TextBox (Tab to focus):"),
                    new TextBoxWidget(session.SampleTextBoxState),
                    new TextBlockWidget(""),
                    new TextBlockWidget("Button:"),
                    new ButtonWidget(
                        session.ButtonClicked ? "Clicked!" : "Click Me", 
                        () => session.ButtonClicked = !session.ButtonClicked
                    ),
                    new TextBlockWidget(""),
                    new TextBlockWidget("─────────────────────────"),
                    new TextBlockWidget(""),
                    new TextBlockWidget("Use ↑↓ to change theme"),
                    new TextBlockWidget("Tab to switch focus"),
                    new TextBlockWidget("Enter to click button"),
                ]),
                LeftWidth: 20
            )
        );
    }

    public override Func<Hex1bTheme>? CreateThemeProvider()
    {
        // Use the session state created by CreateWidgetBuilder
        var session = _currentSession!;
        return () => session.Themes[session.ThemeListState.SelectedIndex];
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
            .Set(SplitterTheme.DividerCharacter, "┃");
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
            .Set(SplitterTheme.DividerCharacter, "║");
    }

    private class ThemingSessionState
    {
        public required Hex1bTheme[] Themes { get; init; }
        public required ListState ThemeListState { get; init; }
        public required TextBoxState SampleTextBoxState { get; init; }
        public bool ButtonClicked { get; set; }
    }
}
