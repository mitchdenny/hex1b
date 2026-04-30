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
    private readonly CloudShellWidget _cloudShell;
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

    public ShellScreen(AppState appState, CloudShellWidget cloudShell, TutorialService tutorial, PanelManager panelManager)
    {
        _appState = appState;
        _cloudShell = cloudShell;
        _tutorial = tutorial;
        _panelManager = panelManager;
    }

    public Hex1bWidget Build<TParent>(WidgetContext<TParent> ctx, Hex1bApp app)
        where TParent : Hex1bWidget
    {
        _panelManager.SetApp(app);

        // Register cloud shell and tutorial panels
        _panelManager.SetCloudShellPanel();
        _panelManager.SetTutorialPanel();

        // Update tutorial panel title with step count
        var tutorialPanel = _panelManager.Panels.FirstOrDefault(p => p.Tag == "tutorial");
        if (tutorialPanel != null)
            tutorialPanel.Title = $"Tutorial ({_tutorial.CurrentStep + 1}/{_tutorial.TotalSteps})";

        return ctx.ZStack(z =>
        [
            // Base content
            z.VStack(v =>
            [
                // Panel area — HStack showing only viewport-visible panels
                v.HStack(h =>
                {
                    var panels = new List<Hex1bWidget>();

                    foreach (var (panel, idx) in _panelManager.VisiblePanels)
                    {
                        var isFocused = idx == _panelManager.FocusedIndex;

                        Hex1bWidget panelWidget;
                        if (panel.Tag == "cloud-shell")
                            panelWidget = BuildCloudShellPanel(h, panel.Title, isFocused, app);
                        else if (panel.Tag == "tutorial")
                            panelWidget = BuildTutorialPanel(h, isFocused);
                        else if (panel.Tag == "data-browser")
                            panelWidget = BuildDataBrowserPanel(h, panel, isFocused);
                        else if (panel.IsTerminal)
                            panelWidget = BuildTerminalPanel(h, panel.Title, panel.Handle!, isFocused);
                        else
                            panelWidget = BuildEmptyPanel(h, panel.Title, isFocused);

                        panels.Add(panelWidget.FillWidth(1));
                    }

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
                    s.Section("Zoom"),
                    s.Separator("  "),
                    s.Section("Ctrl+Z PgUp/Dn"),
                    s.Separator(" "),
                    s.Section("Focus"),
                    s.Separator("  "),
                    s.Section("Ctrl+Z Q"),
                    s.Separator(" "),
                    s.Section("Close"),
                    s.Spacer(),
                    s.Section($"[{_panelManager.FocusedIndex + 1}/{_panelManager.PanelCount}] Zoom:{_panelManager.Zoom}"),
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

            // Ctrl+Z then → to zoom out (show more panels)
            bindings.Ctrl().Key(Hex1bKey.Z).Then().Key(Hex1bKey.RightArrow)
                .OverridesCapture()
                .Action(_ => _panelManager.ZoomOut(), "Zoom out");

            // Ctrl+Z then ← to zoom in (show fewer panels)
            bindings.Ctrl().Key(Hex1bKey.Z).Then().Key(Hex1bKey.LeftArrow)
                .OverridesCapture()
                .Action(_ => _panelManager.ZoomIn(), "Zoom in");

            bindings.Ctrl().Key(Hex1bKey.Z).Then().Key(Hex1bKey.PageDown)
                .OverridesCapture()
                .Action(_ => _panelManager.FocusNext(), "Focus next panel");

            bindings.Ctrl().Key(Hex1bKey.Z).Then().Key(Hex1bKey.PageUp)
                .OverridesCapture()
                .Action(_ => _panelManager.FocusPrevious(), "Focus previous panel");

            bindings.Ctrl().Key(Hex1bKey.Z).Then().Key(Hex1bKey.Q)
                .OverridesCapture()
                .Action(async _ => await _panelManager.CloseCurrentPanelAsync(), "Close panel");

            // Ctrl+Z then O to open data browser to the right with last ls result
            bindings.Ctrl().Key(Hex1bKey.Z).Then().Key(Hex1bKey.O)
                .OverridesCapture()
                .Action(_ =>
                {
                    var data = _cloudShell.LastResourceList;
                    if (data != null)
                        _panelManager.InsertPanelRight("Data Browser", tag: "data-browser", data: data);
                }, "Open data browser");
        });
    }

    private Hex1bWidget BuildTerminalPanel<TParent>(
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

    private Hex1bWidget BuildTutorialPanel<TParent>(WidgetContext<TParent> ctx, bool isFocused)
        where TParent : Hex1bWidget
    {
        var borderColor = isFocused
            ? Hex1bColor.FromRgb(60, 130, 255)
            : Hex1bColor.Gray;

        return ctx.ThemePanel(
            t => t.Set(BorderTheme.BorderColor, borderColor),
            ctx.Border(b =>
            [
                b.VScrollPanel(sp =>
                [
                    sp.Markdown(_tutorial.GetCurrentMarkdown()),
                ]).Fill(),
            ]).Title($"Tutorial ({_tutorial.CurrentStep + 1}/{_tutorial.TotalSteps})").Fill()
        );
    }

    private Hex1bWidget BuildCloudShellPanel<TParent>(
        WidgetContext<TParent> ctx, string title, bool isFocused, Hex1bApp app)
        where TParent : Hex1bWidget
    {
        var borderColor = isFocused
            ? Hex1bColor.FromRgb(60, 130, 255)
            : Hex1bColor.Gray;

        return ctx.ThemePanel(
            t => t.Set(BorderTheme.BorderColor, borderColor),
            ctx.Border(b =>
            [
                _cloudShell.Build(b, app).Fill(),
            ]).Title(title).Fill()
        );
    }

    private static Hex1bWidget BuildEmptyPanel<TParent>(
        WidgetContext<TParent> ctx, string title, bool isFocused)
        where TParent : Hex1bWidget
    {
        var borderColor = isFocused
            ? Hex1bColor.FromRgb(60, 130, 255)
            : Hex1bColor.Gray;

        return ctx.ThemePanel(
            t => t.Set(BorderTheme.BorderColor, borderColor),
            ctx.Border(b =>
            [
                b.Text("").Fill(),
            ]).Title(title).Fill()
        );
    }

    private Hex1bWidget BuildDataBrowserPanel<TParent>(
        WidgetContext<TParent> ctx, ShellPanel panel, bool isFocused)
        where TParent : Hex1bWidget
    {
        var borderColor = isFocused
            ? Hex1bColor.FromRgb(60, 130, 255)
            : Hex1bColor.Gray;

        var data = panel.Data as ResourceListResult;

        return ctx.ThemePanel(
            t => t.Set(BorderTheme.BorderColor, borderColor),
            ctx.Border(b =>
            [
                data != null
                    ? b.Table((IReadOnlyList<ResourceListResult.ResourceRow>)data.Rows)
                        .Header(h =>
                        [
                            h.Cell("Name").Width(SizeHint.Fill),
                            h.Cell("Type").Width(SizeHint.Fixed(18)),
                            h.Cell("Details").Width(SizeHint.Fill),
                        ])
                        .Row((r, row, state) =>
                        [
                            r.Cell(row.Name),
                            r.Cell(row.Type),
                            r.Cell(row.Description ?? ""),
                        ])
                        .RowKey(r => r.Name)
                        .FillWidth()
                        .FillHeight()
                    : b.Text("  No data.").Fill(),
            ]).Title(panel.Title).Fill()
        );
    }
}
