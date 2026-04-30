using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace CloudTermDemo;

/// <summary>
/// Main shell screen with a cloud terminal panel and a tutorial panel
/// arranged side by side, plus an InfoBar at the bottom.
/// F1 toggles a help overlay.
/// </summary>
public sealed class ShellScreen
{
    private readonly AppState _appState;
    private readonly CloudTerminalHost _terminalHost;
    private readonly TutorialService _tutorial;

    private readonly PanelManager _panelManager;

    private bool _showHelp;

    private static readonly string HelpMarkdown = """
        # Cloud Term Help

        ## Navigation Commands

        | Command | Description |
        |---------|-------------|
        | `ls` | List resources at the current level |
        | `cd <name>` | Navigate into a resource |
        | `cd ..` | Go up one level |
        | `cd /` | Go to root |
        | `pwd` | Show current path |
        | `help` | Show command help |

        ## Keyboard Shortcuts

        | Key | Action |
        |-----|--------|
        | **F1** | Toggle this help panel |
        | **Tab** | Switch focus between panels |
        | **Escape** | Close help / dismiss overlay |

        ## Resource Hierarchy

        Your cloud environment is organized as:

        **User** → **Tenant** → **Subscription** → **Resource Group** → **Resource**

        Some resources (like AKS clusters) have sub-resources you can
        drill into.

        ---

        Press **Escape** to close this help panel.
        """;

    public ShellScreen(AppState appState, CloudTerminalHost terminalHost, TutorialService tutorial, PanelManager panelManager)
    {
        _appState = appState;
        _terminalHost = terminalHost;
        _tutorial = tutorial;
        _panelManager = panelManager;
    }

    public Hex1bWidget Build<TParent>(WidgetContext<TParent> ctx, Hex1bApp app)
        where TParent : Hex1bWidget
    {
        _terminalHost.Start();
        _panelManager.SetApp(app);

        // Register the cloud shell as panel 0 if not already
        if (_terminalHost.Handle != null)
            _panelManager.SetCloudShellPanel(_terminalHost.Handle);

        return ctx.ZStack(z =>
        [
            // Base content
            z.VStack(v =>
            [
                // Panel area — HStack with weighted fills
                v.HStack(h =>
                {
                    var panels = new List<Hex1bWidget>();

                    // Terminal panels (cloud shell + dynamic)
                    for (var i = 0; i < _panelManager.PanelCount; i++)
                    {
                        var panel = _panelManager.Panels[i];
                        var isFocused = i == _panelManager.FocusedIndex;
                        var panelWidget = BuildPanel(h, panel.Title, panel.Handle, isFocused);
                        panels.Add(panelWidget.FillWidth(panel.Weight));
                    }

                    // Tutorial panel (always rightmost, not in PanelManager)
                    panels.Add(
                        BuildTutorialPanel(h).FillWidth(1)
                    );

                    return panels.ToArray();
                }).Fill(),

                // Info bar
                v.InfoBar(s =>
                [
                    s.Section("F1"),
                    s.Separator(" "),
                    s.Section("Help"),
                    s.Separator("  "),
                    s.Section("Ctrl+Z ←/→"),
                    s.Separator(" "),
                    s.Section("Resize"),
                    s.Separator("  "),
                    s.Section("Ctrl+Z PgUp/Dn"),
                    s.Separator(" "),
                    s.Section("Focus"),
                    s.Spacer(),
                    s.Section(_appState.StatusMessage),
                ]),
            ]),

            // Help overlay
            _showHelp
                ? z.Backdrop(
                    z.Padding(4, 4, 2, 2,
                        z.Border(b =>
                        [
                            b.VScrollPanel(sp =>
                            [
                                sp.Markdown(HelpMarkdown),
                            ]).Fill(),
                        ]).Title("Help (Esc to close)").Fill()
                    )
                  )
                  .OnClickAway(() => { _showHelp = false; app.Invalidate(); })
                : null,
        ]).WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.F1).Global()
                .OverridesCapture()
                .Action(_ =>
                {
                    _showHelp = !_showHelp;
                    app.Invalidate();
                }, "Toggle help");

            // Ctrl+Z then → to expand focused panel
            bindings.Ctrl().Key(Hex1bKey.Z).Then().Key(Hex1bKey.RightArrow)
                .OverridesCapture()
                .Action(_ => _panelManager.ExpandFocused(), "Expand panel");

            // Ctrl+Z then ← to shrink focused panel
            bindings.Ctrl().Key(Hex1bKey.Z).Then().Key(Hex1bKey.LeftArrow)
                .OverridesCapture()
                .Action(_ => _panelManager.ShrinkFocused(), "Shrink panel");

            // Ctrl+Z then PgDown to focus next panel
            bindings.Ctrl().Key(Hex1bKey.Z).Then().Key(Hex1bKey.PageDown)
                .OverridesCapture()
                .Action(_ => _panelManager.FocusNext(), "Focus next panel");

            // Ctrl+Z then PgUp to focus previous panel
            bindings.Ctrl().Key(Hex1bKey.Z).Then().Key(Hex1bKey.PageUp)
                .OverridesCapture()
                .Action(_ => _panelManager.FocusPrevious(), "Focus previous panel");
        });
    }

    private Hex1bWidget BuildPanel<TParent>(
        WidgetContext<TParent> ctx,
        string title,
        TerminalWidgetHandle handle,
        bool isFocused)
        where TParent : Hex1bWidget
    {
        var borderColor = isFocused
            ? Hex1bColor.FromRgb(60, 130, 255)
            : Hex1bColor.Gray;

        return ctx.ThemePanel(
            t => t.Set(BorderTheme.BorderColor, borderColor),
            ctx.Border(b =>
            [
                b.Terminal(handle).Fill(),
            ]).Title(title).Fill()
        );
    }

    private Hex1bWidget BuildTutorialPanel<TParent>(WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
    {
        return ctx.Border(b =>
        [
            b.VScrollPanel(sp =>
            [
                sp.Markdown(_tutorial.GetCurrentMarkdown()),
            ]).Fill(),
        ]).Title($"Tutorial ({_tutorial.CurrentStep + 1}/{_tutorial.TotalSteps})").Fill();
    }
}
