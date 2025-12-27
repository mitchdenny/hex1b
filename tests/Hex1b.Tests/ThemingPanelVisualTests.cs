using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Terminal;
using Hex1b.Terminal.Testing;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Visual tests for ThemingPanel that verify scoped theming works correctly.
/// These tests match the examples from the theming-panel.md documentation and
/// generate SVG, HTML, ANSI captures as evidence.
/// </summary>
public class ThemingPanelVisualTests
{
    // DarkBlue color for background tests (not a predefined static like Hex1bColor.Blue)
    private static readonly Hex1bColor DarkBlue = Hex1bColor.FromRgb(0, 0, 139);
    
    /// <summary>
    /// Tests the "basicCode" example from theming-panel.md:
    /// Button with cyan focus colors in a ThemingPanel.
    /// </summary>
    [Fact]
    public async Task BasicCode_ButtonWithCyanFocus_RendersFocusedWithCyanBackground()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.ThemingPanel(
                    theme => theme
                        .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Cyan)
                        .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.Black),
                    ctx.VStack(v => [
                        v.Text("Buttons in this panel have cyan focus:"),
                        v.Button("Styled Button"),
                        v.Button("Another Button")
                    ])
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Wait for render, then capture initial focused state
        var initialSnapshot = await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Styled Button"), TimeSpan.FromSeconds(2))
            .Capture("theming-panel-basic-cyan-focus-initial")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Verify button text is present
        Assert.True(initialSnapshot.ContainsText("Styled Button"));
        Assert.True(initialSnapshot.ContainsText("Another Button"));
        
        // Verify cyan background color is applied to the focused button
        // Cyan = FromRgb(0, 255, 255) - the button should have this background
        Assert.True(initialSnapshot.HasBackgroundColor(Hex1bColor.Cyan), 
            "First button should have cyan background when focused");

        // Navigate to second button and verify theming persists
        var secondButtonSnapshot = await new Hex1bTestSequenceBuilder()
            .Tab()
            .Wait(100)
            .Capture("theming-panel-basic-cyan-focus-second")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
            
        await runTask;
        
        // Verify cyan is still applied for second button
        Assert.True(secondButtonSnapshot.HasBackgroundColor(Hex1bColor.Cyan),
            "Second button should also have cyan background when focused");
    }
    
    /// <summary>
    /// Tests the "interactiveCode" example from theming-panel.md:
    /// Side-by-side theme comparison with Standard, Success (green), Danger (red).
    /// </summary>
    [Fact]
    public async Task InteractiveCode_SideBySideThemes_EachSectionHasCorrectColors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 15);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    // Normal theme section
                    h.Border(b => [
                        b.VStack(v => [
                            v.Text("Default Theme"),
                            v.Button("Normal"),
                            v.Button("Buttons")
                        ])
                    ], title: "Standard"),
                    
                    // Custom themed section (Success/Green)
                    h.ThemingPanel(
                        theme => theme
                            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Green)
                            .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.Black)
                            .Set(BorderTheme.BorderColor, Hex1bColor.Green),
                        h.Border(b => [
                            b.VStack(v => [
                                v.Text("Success Theme"),
                                v.Button("Green"),
                                v.Button("Buttons")
                            ])
                        ], title: "Styled")
                    ),
                    
                    // Another custom themed section (Danger/Red)
                    h.ThemingPanel(
                        theme => theme
                            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Red)
                            .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.White)
                            .Set(BorderTheme.BorderColor, Hex1bColor.Red),
                        h.Border(b => [
                            b.VStack(v => [
                                v.Text("Danger Theme"),
                                v.Button("Red"),
                                v.Button("Buttons")
                            ])
                        ], title: "Warning")
                    )
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Wait for render and capture initial state (focus on first button in Standard)
        var standardFocused = await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Default Theme"), TimeSpan.FromSeconds(2))
            .Capture("theming-panel-interactive-standard")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Verify all sections are rendered
        Assert.True(standardFocused.ContainsText("Default Theme"));
        Assert.True(standardFocused.ContainsText("Success Theme"));
        Assert.True(standardFocused.ContainsText("Danger Theme"));
        
        // Navigate to Green section buttons (Tab forward past Standard buttons)
        // Standard has 2 buttons, so we need to go to button 3 (first in Success section)
        var successFocused = await new Hex1bTestSequenceBuilder()
            .Tab()  // Button 2 in Standard
            .Tab()  // Button 1 in Success
            .Wait(100)
            .Capture("theming-panel-interactive-success")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Verify green is applied in Success section
        Assert.True(successFocused.HasBackgroundColor(Hex1bColor.Green),
            "Button in Success section should have green background when focused");
        
        // Navigate to Red/Danger section buttons
        var dangerFocused = await new Hex1bTestSequenceBuilder()
            .Tab()  // Button 2 in Success
            .Tab()  // Button 1 in Danger
            .Wait(100)
            .Capture("theming-panel-interactive-danger")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Verify red is applied in Danger section
        Assert.True(dangerFocused.HasBackgroundColor(Hex1bColor.Red),
            "Button in Danger section should have red background when focused");
            
        await runTask;
    }
    
    /// <summary>
    /// Tests nested ThemingPanels where inner panel overrides outer panel theme.
    /// </summary>
    [Fact]
    public async Task NestedThemingPanels_InnerOverridesOuter()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.ThemingPanel(
                    theme => theme.Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Blue),
                    ctx.VStack(v => [
                        v.Button("Blue focus"),
                        v.ThemingPanel(
                            innerTheme => innerTheme.Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Red),
                            v.Button("Red focus (nested override)")
                        ),
                        v.Button("Blue focus again")
                    ])
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Wait for render, first button (Blue) is focused
        var firstBlue = await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Blue focus"), TimeSpan.FromSeconds(2))
            .Capture("theming-panel-nested-first-blue")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.True(firstBlue.HasBackgroundColor(Hex1bColor.Blue),
            "First button should have blue background (outer ThemingPanel)");
        
        // Navigate to nested button (Red)
        var nestedRed = await new Hex1bTestSequenceBuilder()
            .Tab()
            .Wait(100)
            .Capture("theming-panel-nested-red")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.True(nestedRed.HasBackgroundColor(Hex1bColor.Red),
            "Nested button should have red background (inner ThemingPanel override)");
        
        // Navigate to third button (Blue again)
        var thirdBlue = await new Hex1bTestSequenceBuilder()
            .Tab()
            .Wait(100)
            .Capture("theming-panel-nested-third-blue")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.True(thirdBlue.HasBackgroundColor(Hex1bColor.Blue),
            "Third button should have blue background (back to outer ThemingPanel)");
            
        await runTask;
    }
    
    /// <summary>
    /// Tests border theming via ThemingPanel with green border color.
    /// </summary>
    [Fact]
    public async Task BorderTheming_GreenBorderColor_AppliesCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.ThemingPanel(
                    theme => theme
                        .Set(BorderTheme.BorderColor, Hex1bColor.Green)
                        .Set(BorderTheme.TitleColor, Hex1bColor.White),
                    ctx.Border(b => [
                        b.VStack(v => [
                            v.Text("Content inside green border"),
                            v.Button("Button")
                        ])
                    ], title: "Green Box")
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        var snapshot = await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Green Box"), TimeSpan.FromSeconds(2))
            .Capture("theming-panel-green-border")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
            
        await runTask;
        
        // Verify green is used in the output (border color)
        Assert.True(snapshot.HasForegroundColor(Hex1bColor.Green),
            "Border should have green foreground color");
        Assert.True(snapshot.ContainsText("Green Box"));
        Assert.True(snapshot.ContainsText("┌") || snapshot.ContainsText("╭"));
    }
    
    /// <summary>
    /// Tests List widget theming via ThemingPanel with yellow selection.
    /// </summary>
    [Fact]
    public async Task ListTheming_YellowSelection_AppliesCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 10);

        IReadOnlyList<string> items = ["Option A", "Option B", "Option C"];

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.ThemingPanel(
                    theme => theme
                        .Set(ListTheme.SelectedForegroundColor, Hex1bColor.Black)
                        .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.Yellow)
                        .Set(ListTheme.SelectedIndicator, "→ ")
                        .Set(ListTheme.UnselectedIndicator, "  "),
                    ctx.List(items)
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        var initialSnapshot = await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Option A"), TimeSpan.FromSeconds(2))
            .Capture("theming-panel-list-yellow-initial")
            .Down()
            .Wait(100)
            .Capture("theming-panel-list-yellow-second")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
            
        await runTask;
        
        // Verify yellow background is applied to selected item
        Assert.True(initialSnapshot.HasBackgroundColor(Hex1bColor.Yellow),
            "Selected list item should have yellow background");
        
        // Verify custom indicator is used
        Assert.True(initialSnapshot.ContainsText("→"));
    }
    
    /// <summary>
    /// Tests ThemingPanel background color fills correctly.
    /// </summary>
    [Fact]
    public async Task BackgroundColor_DarkBlue_FillsCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.ThemingPanel(
                    theme => theme
                        .Set(ThemingPanelTheme.BackgroundColor, DarkBlue)
                        .Set(ThemingPanelTheme.ForegroundColor, Hex1bColor.White),
                    ctx.Text("White text on dark blue background")
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        var snapshot = await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("White text on dark blue"), TimeSpan.FromSeconds(2))
            .Capture("theming-panel-dark-blue-background")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
            
        await runTask;
        
        Assert.True(snapshot.ContainsText("White text on dark blue"));
        Assert.True(snapshot.HasBackgroundColor(DarkBlue),
            "Panel should have dark blue background");
    }
    
    /// <summary>
    /// Tests semantic sections pattern with Primary (green) and Destructive (red) areas.
    /// </summary>
    [Fact]
    public async Task SemanticSections_PrimaryAndDestructive_RenderCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 15);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    // Primary action area
                    v.ThemingPanel(
                        theme => theme
                            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Green)
                            .Set(BorderTheme.BorderColor, Hex1bColor.Green),
                        v.Border(b => [b.Button("Confirm")], title: "Primary")
                    ),
                    
                    // Destructive action area  
                    v.ThemingPanel(
                        theme => theme
                            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Red)
                            .Set(BorderTheme.BorderColor, Hex1bColor.Red),
                        v.Border(b => [b.Button("Delete")], title: "Danger")
                    )
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Initial: Focus on Confirm button (green)
        var primaryFocused = await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Confirm"), TimeSpan.FromSeconds(2))
            .Capture("theming-panel-semantic-primary")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.True(primaryFocused.HasBackgroundColor(Hex1bColor.Green),
            "Confirm button should have green background when focused");
        
        // Navigate to Delete button (red)
        var dangerFocused = await new Hex1bTestSequenceBuilder()
            .Tab()
            .Wait(100)
            .Capture("theming-panel-semantic-danger")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
            
        await runTask;
        
        Assert.True(dangerFocused.HasBackgroundColor(Hex1bColor.Red),
            "Delete button should have red background when focused");
    }
}
