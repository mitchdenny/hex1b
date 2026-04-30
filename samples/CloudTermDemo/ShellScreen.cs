using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

namespace CloudTermDemo;

/// <summary>
/// Main shell screen with a cloud terminal panel and a tutorial panel
/// arranged side by side, plus an InfoBar at the bottom.
/// </summary>
public sealed class ShellScreen
{
    private readonly AppState _appState;
    private readonly CloudTerminalHost _terminalHost;
    private readonly TutorialService _tutorial;

    public ShellScreen(AppState appState, CloudTerminalHost terminalHost, TutorialService tutorial)
    {
        _appState = appState;
        _terminalHost = terminalHost;
        _tutorial = tutorial;
    }

    public Hex1bWidget Build<TParent>(WidgetContext<TParent> ctx, Hex1bApp app)
        where TParent : Hex1bWidget
    {
        // Ensure the cloud terminal is running
        _terminalHost.Start();

        var handle = _terminalHost.Handle;

        return ctx.VStack(v =>
        [
            // Main content: cloud terminal (left) + tutorial (right)
            v.HSplitter(
                // Left panel: cloud terminal
                CloudShellPanel.Build(v, "Cloud Shell", b =>
                [
                    handle != null
                        ? b.Terminal(handle).Fill()
                        : b.Text("Starting terminal...").Fill(),
                ]),
                // Right panel: tutorial (markdown)
                CloudShellPanel.Build(v, $"Tutorial ({_tutorial.CurrentStep + 1}/{_tutorial.TotalSteps})", b =>
                [
                    b.VScrollPanel(sp =>
                    [
                        sp.Markdown(_tutorial.GetCurrentMarkdown()),
                    ]).Fill(),
                ]),
                leftWidth: 60
            ).Fill(),

            // Info bar
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
        ]).WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.F1).Global().Action(_ =>
            {
                _appState.StatusMessage = _appState.StatusMessage == "Help: F1"
                    ? "Ready"
                    : "Help: F1";
                app.Invalidate();
            }, "Toggle help");
        });
    }
}
