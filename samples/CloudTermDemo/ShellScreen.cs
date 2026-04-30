using Hex1b;
using Hex1b.Documents;
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
    private readonly Dictionary<ShellPanel, string?> _savingPanels = new();

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
                        else if (panel.Tag == "editor")
                            panelWidget = BuildEditorPanel(h, panel, isFocused, app);
                        else if (panel.IsTerminal)
                            panelWidget = BuildTerminalPanel(h, panel.Title, panel.Handle!, isFocused);
                        else
                            panelWidget = BuildEmptyPanel(h, panel.Title, isFocused);

                        panels.Add(panelWidget.FillWidth(1));
                    }

                    return panels.ToArray();
                }).Fill(),

                // Info bar — context-sensitive
                v.InfoBar(s =>
                {
                    var sections = new List<IInfoBarChild>();

                    sections.Add(s.Section("F1"));
                    sections.Add(s.Separator(" "));
                    sections.Add(s.Section("Help"));
                    sections.Add(s.Separator("  "));

                    // Context-sensitive shortcuts for focused panel
                    var focusedPanel = _panelManager.FocusedIndex >= 0 && _panelManager.FocusedIndex < _panelManager.PanelCount
                        ? _panelManager.Panels[_panelManager.FocusedIndex] : null;

                    if (focusedPanel?.Tag == "editor")
                    {
                        sections.Add(s.Section("Ctrl+S"));
                        sections.Add(s.Separator(" "));
                        sections.Add(s.Section("Save"));
                        sections.Add(s.Separator("  "));
                    }

                    sections.Add(s.Section("Ctrl+P ←/→"));
                    sections.Add(s.Separator(" "));
                    sections.Add(s.Section("Zoom"));
                    sections.Add(s.Separator("  "));
                    sections.Add(s.Section("Ctrl+P PgUp/Dn"));
                    sections.Add(s.Separator(" "));
                    sections.Add(s.Section("Focus"));
                    sections.Add(s.Separator("  "));
                    sections.Add(s.Section("Ctrl+P Q"));
                    sections.Add(s.Separator(" "));
                    sections.Add(s.Section("Close"));
                    sections.Add(s.Spacer());
                    sections.Add(s.Section($"[{_panelManager.FocusedIndex + 1}/{_panelManager.PanelCount}] Zoom:{_panelManager.Zoom}"));

                    return sections;
                }),
            ]),

            // Help overlay
            _showHelp
                ? z.Backdrop(
                    z.Padding(8, 8, 3, 3,
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

            // Ctrl+P then → to zoom out (show more panels)
            bindings.Ctrl().Key(Hex1bKey.P).Then().Key(Hex1bKey.RightArrow)
                .OverridesCapture()
                .Action(_ => _panelManager.ZoomOut(), "Zoom out");

            // Ctrl+P then ← to zoom in (show fewer panels)
            bindings.Ctrl().Key(Hex1bKey.P).Then().Key(Hex1bKey.LeftArrow)
                .OverridesCapture()
                .Action(_ => _panelManager.ZoomIn(), "Zoom in");

            bindings.Ctrl().Key(Hex1bKey.P).Then().Key(Hex1bKey.PageDown)
                .OverridesCapture()
                .Action(_ => _panelManager.FocusNext(), "Focus next panel");

            bindings.Ctrl().Key(Hex1bKey.P).Then().Key(Hex1bKey.PageUp)
                .OverridesCapture()
                .Action(_ => _panelManager.FocusPrevious(), "Focus previous panel");

            bindings.Ctrl().Key(Hex1bKey.P).Then().Key(Hex1bKey.Q)
                .OverridesCapture()
                .Action(async _ => await _panelManager.CloseCurrentPanelAsync(), "Close panel");

            // Ctrl+P then O to open data browser to the right with last ls result
            bindings.Ctrl().Key(Hex1bKey.P).Then().Key(Hex1bKey.O)
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

    private Hex1bWidget BuildEditorPanel<TParent>(
        WidgetContext<TParent> ctx, ShellPanel panel, bool isFocused, Hex1bApp app)
        where TParent : Hex1bWidget
    {
        var borderColor = isFocused
            ? Hex1bColor.FromRgb(60, 130, 255)
            : Hex1bColor.Gray;

        var (editorState, highlighter) = panel.Data is (EditorState es, YamlSyntaxHighlighter yh)
            ? (es, yh)
            : (new EditorState(new Hex1bDocument("")), new YamlSyntaxHighlighter());

        var isSaving = _savingPanels.TryGetValue(panel, out var saveMessage);

        return ctx.ThemePanel(
            t => t.Set(BorderTheme.BorderColor, borderColor),
            ctx.Border(b =>
            [
                b.VStack(v =>
                {
                    var widgets = new List<Hex1bWidget>();

                    // Save banner
                    if (isSaving)
                    {
                        widgets.Add(
                            v.ThemePanel(
                                t => t
                                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.White)
                                    .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(40, 100, 200)),
                                v.HStack(h => [
                                    h.Text(" "),
                                    h.Spinner(SpinnerStyle.Dots),
                                    h.Text($" {saveMessage ?? "Saving..."}"),
                                ]).Height(SizeHint.Content)
                            )
                        );
                    }

                    // Editor
                    widgets.Add(
                        v.Editor(editorState)
                            .LineNumbers()
                            .Decorations(highlighter)
                            .Fill()
                            .WithInputBindings(bindings =>
                            {
                                bindings.Ctrl().Key(Hex1bKey.S)
                                    .OverridesCapture()
                                    .Action(_ =>
                                    {
                                        if (!_savingPanels.ContainsKey(panel))
                                        {
                                            var capturedPanel = panel;
                                            Task.Run(async () => await SaveAsync(capturedPanel, app));
                                        }
                                    }, "Save");
                            })
                    );

                    return widgets.ToArray();
                }).Fill(),
            ]).Title(panel.Title).Fill()
        );
    }

    private async Task SaveAsync(ShellPanel panel, Hex1bApp app)
    {
        _savingPanels[panel] = "Saving to cluster...";
        app.Invalidate();

        await Task.Delay(800);

        _savingPanels[panel] = "Validating YAML...";
        app.Invalidate();

        await Task.Delay(600);

        _savingPanels[panel] = "Applying changes...";
        app.Invalidate();

        await Task.Delay(400);

        _savingPanels.Remove(panel);
        app.Invalidate();
    }
}
