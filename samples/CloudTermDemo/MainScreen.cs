using Hex1b;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace CloudTermDemo;

/// <summary>
/// Main application shell displayed after the splash screen.
/// Provides title bar, content area, and status bar.
/// </summary>
public sealed class MainScreen
{
    private readonly AppState _appState;

    public MainScreen(AppState appState)
    {
        _appState = appState;
    }

    public Hex1bWidget Build<TParent>(WidgetContext<TParent> ctx, Hex1bApp app)
        where TParent : Hex1bWidget
    {
        return ctx.VStack(v => [
            // Title bar
            v.HStack(h => [
                h.Text(" ☁ Cloud Terminal"),
                h.Text("  |  v0.1.0-preview"),
            ]).Height(SizeHint.Content),

            v.Separator(),

            // Main content area
            v.VStack(content => [
                content.Text(""),
                content.Text("  Welcome to Cloud Terminal"),
                content.Text(""),
                content.Text("  A rich terminal experience for cloud resource management."),
                content.Text("  This demo explores what a full TUI could look like."),
                content.Text(""),
                content.Text("  Press Ctrl+C to exit."),
            ]).Fill(),

            v.Separator(),

            // Status bar
            v.InfoBar([
                "Status", _appState.StatusMessage,
                "Screen", _appState.CurrentScreen.ToString(),
            ]),
        ]);
    }
}
