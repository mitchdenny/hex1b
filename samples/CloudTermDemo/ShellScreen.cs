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

        var handle = _terminalHost.Handle;

        // Build the left content: cloud shell + any dynamic terminal panels
        Hex1bWidget leftContent;
        if (_panelManager.Panels.Count == 0)
        {
            leftContent = CloudShellPanel.Build(ctx, "Cloud Shell", b =>
            [
                handle != null
                    ? b.Terminal(handle).Fill()
                    : b.Text("Starting terminal...").Fill(),
            ]);
        }
        else
        {
            // Stack the cloud shell and dynamic panels horizontally
            var cloudShell = CloudShellPanel.Build(ctx, "Cloud Shell", b =>
            [
                handle != null
                    ? b.Terminal(handle).Fill()
                    : b.Text("Starting terminal...").Fill(),
            ]);

            // Chain HSplitters for each additional panel
            leftContent = cloudShell;
            foreach (var panel in _panelManager.Panels)
            {
                var panelHandle = panel.Handle;
                var panelTitle = panel.Title;
                leftContent = ctx.HSplitter(
                    leftContent,
                    CloudShellPanel.Build(ctx, panelTitle, b =>
                    [
                        b.Terminal(panelHandle).Fill(),
                    ]),
                    leftWidth: 50
                );
            }
            leftContent = leftContent.Fill();
        }

        return ctx.ZStack(z =>
        [
            // Base content
            z.VStack(v =>
            [
                v.HSplitter(
                    leftContent,
                    CloudShellPanel.Build(v, $"Tutorial ({_tutorial.CurrentStep + 1}/{_tutorial.TotalSteps})", b =>
                    [
                        b.VScrollPanel(sp =>
                        [
                            sp.Markdown(_tutorial.GetCurrentMarkdown()),
                        ]).Fill(),
                    ]),
                    leftWidth: 60
                ).Fill(),

                v.InfoBar(s =>
                [
                    s.Section("F1"),
                    s.Separator(" "),
                    s.Section("Help"),
                    s.Separator("  "),
                    s.Section("Tab"),
                    s.Separator(" "),
                    s.Section("Switch Panel"),
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
            bindings.Key(Hex1bKey.F1).Global().Action(_ =>
            {
                _showHelp = !_showHelp;
                app.Invalidate();
            }, "Toggle help");
        });
    }
}
