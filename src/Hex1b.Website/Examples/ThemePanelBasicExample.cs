using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// ThemePanel Widget Documentation: Dangerous Settings Panel
/// Demonstrates scoped theme mutations with a realistic settings UI.
/// </summary>
public class ThemePanelBasicExample(ILogger<ThemePanelBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<ThemePanelBasicExample> _logger = logger;

    public override string Id => "themepanel-basic";
    public override string Title => "ThemePanel - Dangerous Settings";
    public override string Description => "A settings panel with dangerous options styled using ThemePanel";

    private class SettingsState
    {
        public bool TelemetryEnabled { get; set; } = true;
        public bool AutoUpdates { get; set; } = true;
        public string DataPath { get; set; } = "/var/data";
        public bool ConfirmDelete { get; set; }
        public bool FactoryReset { get; set; }
        public string StatusMessage { get; set; } = "Ready";
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating themepanel dangerous settings example");

        var state = new SettingsState();

        // Danger zone theme mutator - theme is already cloned by ThemePanel
        Func<Hex1bTheme, Hex1bTheme> dangerTheme = theme => theme
            .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(255, 100, 100))
            .Set(BorderTheme.BorderColor, Hex1bColor.FromRgb(180, 0, 0))
            .Set(BorderTheme.TitleColor, Hex1bColor.Red)
            .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(100, 0, 0))
            .Set(ButtonTheme.ForegroundColor, Hex1bColor.White)
            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Red)
            .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.White)
            .Set(ToggleSwitchTheme.FocusedSelectedBackgroundColor, Hex1bColor.Red)
            .Set(ToggleSwitchTheme.UnfocusedSelectedBackgroundColor, Hex1bColor.FromRgb(100, 0, 0))
            .Set(TextBoxTheme.CursorBackgroundColor, Hex1bColor.Red);

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Border(b => [
                    b.Text("  General Settings"),
                    b.Text(""),
                    b.HStack(h => [
                        h.Text("Telemetry:    "),
                        h.ToggleSwitch(["Off", "On"], state.TelemetryEnabled ? 1 : 0)
                            .OnSelectionChanged(e => state.TelemetryEnabled = e.SelectedIndex == 1)
                    ]),
                    b.HStack(h => [
                        h.Text("Auto-updates: "),
                        h.ToggleSwitch(["Off", "On"], state.AutoUpdates ? 1 : 0)
                            .OnSelectionChanged(e => state.AutoUpdates = e.SelectedIndex == 1)
                    ]),
                    b.Text(""),
                    b.Text("  Data Path:"),
                    b.TextBox(state.DataPath).OnTextChanged(e => state.DataPath = e.NewText)
                ], title: "⚙ Settings"),
                
                v.Text(""),
                
                v.ThemePanel(dangerTheme, danger => [
                    danger.Border(db => [
                        db.Text("  These actions cannot be undone!"),
                        db.Text(""),
                        db.HStack(h => [
                            h.Text("Delete all data: "),
                            h.ToggleSwitch(["No", "Yes"], state.ConfirmDelete ? 1 : 0)
                                .OnSelectionChanged(e => {
                                    state.ConfirmDelete = e.SelectedIndex == 1;
                                    if (state.ConfirmDelete) state.StatusMessage = "⚠ Delete confirmed!";
                                })
                        ]),
                        db.HStack(h => [
                            h.Text("Factory reset:   "),
                            h.ToggleSwitch(["No", "Yes"], state.FactoryReset ? 1 : 0)
                                .OnSelectionChanged(e => {
                                    state.FactoryReset = e.SelectedIndex == 1;
                                    if (state.FactoryReset) state.StatusMessage = "⚠ Reset confirmed!";
                                })
                        ]),
                        db.Text(""),
                        db.Button("☠ Wipe Everything").OnClick(_ => {
                            state.StatusMessage = "💥 All data destroyed!";
                            state.ConfirmDelete = false;
                            state.FactoryReset = false;
                        })
                    ], title: "⚠ DANGER ZONE")
                ]),
                
                v.Text(""),
                v.Text($"Status: {state.StatusMessage}"),
                v.Text(""),
                v.Text("Tab: Navigate  Enter: Activate  ←→: Toggle")
            ]);
        };
    }
}
