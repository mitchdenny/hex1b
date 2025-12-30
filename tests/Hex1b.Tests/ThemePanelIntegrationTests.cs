using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Terminal;
using Hex1b.Terminal.Automation;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Extensive tests for ThemePanelWidget demonstrating theme scoping behavior.
/// These tests verify that theme customizations are correctly applied to child widgets
/// while not affecting widgets outside the ThemePanel.
/// 
/// Test outputs include SVG, HTML, and ANSI evidence files for visual verification.
/// </summary>
public class ThemePanelIntegrationTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string GetTempFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hex1b_themepanel_test_{Guid.NewGuid()}.cast");
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { }
        }
    }

    #region Theme Scoping Tests - Buttons

    /// <summary>
    /// Verifies that button theme customizations inside ThemePanel are applied,
    /// while buttons outside remain with default styling.
    /// </summary>
    [Fact]
    public async Task ThemePanel_ScopesButtonColors_OnlyInsidePanel()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 12);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Button Theme Scoping Test"),
                v.Text(""),
                v.Button("Default Styled Button"),
                v.Text(""),
                v.ThemePanel(
                    theme => theme
                        .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(0, 100, 200))
                        .Set(ButtonTheme.ForegroundColor, Hex1bColor.FromRgb(255, 255, 255))
                        .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(0, 150, 255))
                        .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.FromRgb(0, 0, 0)),
                    ctx.VStack(inner => [
                        inner.Button("Themed Blue Button"),
                        inner.Button("Also Blue")
                    ])
                ),
                v.Text(""),
                v.Button("Back to Default")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Back to Default"), TimeSpan.FromSeconds(2))
            .Capture("themepanel-button-scoping")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // The themed button should have the custom blue background
        Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(0, 100, 200)),
            "Themed button should have blue background");
    }

    /// <summary>
    /// Verifies focused button appearance is correctly themed inside ThemePanel.
    /// </summary>
    [Fact]
    public async Task ThemePanel_AppliesFocusedButtonTheme()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Focused Button Theme Test"),
                v.ThemePanel(
                    theme => theme
                        .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(255, 0, 0))
                        .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.FromRgb(255, 255, 0)),
                    ctx.Button("Focus Me")
                )
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Focus Me"), TimeSpan.FromSeconds(2))
            .Capture("themepanel-focused-button")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // The focused button should have red background (custom focused color)
        Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(255, 0, 0)),
            "Focused button inside ThemePanel should have red background");
    }

    #endregion

    #region Theme Scoping Tests - TextBox

    /// <summary>
    /// Verifies TextBox cursor and selection colors are themed inside ThemePanel.
    /// </summary>
    [Fact]
    public async Task ThemePanel_ScopesTextBoxColors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 12);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("TextBox Theme Scoping Test"),
                v.ThemePanel(
                    theme => theme
                        .Set(TextBoxTheme.CursorBackgroundColor, Hex1bColor.FromRgb(255, 0, 255))
                        .Set(TextBoxTheme.CursorForegroundColor, Hex1bColor.FromRgb(0, 255, 0)),
                    ctx.TextBox("Themed Cursor")
                ),
                v.Text(""),
                v.TextBox("Default TextBox")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // The themed TextBox is first and gets initial focus
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Default TextBox"), TimeSpan.FromSeconds(2))
            .Capture("themepanel-textbox-scoping")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // The themed textbox cursor should have magenta background
        // Note: Cursor is the last character in "Themed Cursor" + a space cursor indicator
        Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(255, 0, 255)),
            "Themed TextBox should have magenta cursor background");
    }

    #endregion

    #region Theme Scoping Tests - List

    /// <summary>
    /// Verifies List selection colors are themed inside ThemePanel.
    /// </summary>
    [Fact]
    public async Task ThemePanel_ScopesListColors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 15);
        IReadOnlyList<string> items = ["Apple", "Banana", "Cherry"];

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("List Theme Scoping Test"),
                v.Text(""),
                v.ThemePanel(
                    theme => theme
                        .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.FromRgb(0, 200, 0))
                        .Set(ListTheme.SelectedForegroundColor, Hex1bColor.FromRgb(0, 0, 0)),
                    ctx.VStack(inner => [
                        inner.Text("Themed List:"),
                        inner.List(items)
                    ])
                )
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Cherry"), TimeSpan.FromSeconds(2))
            .Capture("themepanel-list-scoping")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // The selected list item should have green background
        Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(0, 200, 0)),
            "Selected list item inside ThemePanel should have green background");
    }

    #endregion

    #region Theme Scoping Tests - Border

    /// <summary>
    /// Verifies Border colors are themed inside ThemePanel.
    /// </summary>
    [Fact]
    public async Task ThemePanel_ScopesBorderColors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 15);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Border(ctx.Text("Default Border"), title: "Normal"),
                v.Text(""),
                v.ThemePanel(
                    theme => theme
                        .Set(BorderTheme.BorderColor, Hex1bColor.FromRgb(255, 165, 0))
                        .Set(BorderTheme.TitleColor, Hex1bColor.FromRgb(255, 255, 0)),
                    ctx.Border(ctx.Text("Orange Border"), title: "Themed")
                ),
                v.Text(""),
                v.Border(ctx.Text("Still Default"), title: "After")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Still Default"), TimeSpan.FromSeconds(2))
            .Capture("themepanel-border-scoping")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Should have both default gray and themed orange borders
        Assert.True(snapshot.HasForegroundColor(Hex1bColor.FromRgb(255, 165, 0)),
            "Themed border should have orange color");
    }

    #endregion

    #region Theme Scoping Tests - Progress

    /// <summary>
    /// Verifies Progress bar colors are themed inside ThemePanel.
    /// </summary>
    [Fact]
    public async Task ThemePanel_ScopesProgressColors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 12);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Progress Theme Test"),
                v.Progress(50),
                v.Text(""),
                v.ThemePanel(
                    theme => theme
                        .Set(ProgressTheme.FilledForegroundColor, Hex1bColor.FromRgb(0, 255, 0))
                        .Set(ProgressTheme.EmptyForegroundColor, Hex1bColor.FromRgb(100, 100, 100)),
                    ctx.VStack(inner => [
                        inner.Text("Themed Progress:"),
                        inner.Progress(75)
                    ])
                ),
                v.Text(""),
                v.Progress(25)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Themed Progress"), TimeSpan.FromSeconds(2))
            .Capture("themepanel-progress-scoping")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // The themed progress should have green filled color
        Assert.True(snapshot.HasForegroundColor(Hex1bColor.FromRgb(0, 255, 0)),
            "Themed progress bar should have green filled color");
    }

    #endregion

    #region Nested ThemePanel Tests

    /// <summary>
    /// Verifies that nested ThemePanels apply their themes cumulatively.
    /// </summary>
    [Fact]
    public async Task ThemePanel_NestedPanels_ApplyThemesCumulatively()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 70, 15);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Nested ThemePanel Test"),
                v.ThemePanel(
                    theme => theme
                        .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(0, 0, 200)),
                    ctx.VStack(outer => [
                        outer.Button("Blue (Outer)"),
                        outer.ThemePanel(
                            innerTheme => innerTheme
                                .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(200, 0, 0)),
                            ctx.VStack(inner => [
                                inner.Button("Red (Inner)"),
                                inner.Text("Inner panel overrides outer")
                            ])
                        ),
                        outer.Button("Blue Again (Outer)")
                    ])
                )
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Blue Again"), TimeSpan.FromSeconds(2))
            .Capture("themepanel-nested")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Should have both blue and red buttons
        Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(0, 0, 200)),
            "Outer ThemePanel buttons should be blue");
        Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(200, 0, 0)),
            "Inner ThemePanel button should be red");
    }

    /// <summary>
    /// Verifies nested ThemePanels work exactly as shown in the documentation example.
    /// This matches themepanel-nested.cs snippet: outer cyan text, inner yellow text, back to cyan.
    /// </summary>
    [Fact]
    public async Task ThemePanel_NestedDocExample_CyanYellowCyan()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 8);

        using var app = new Hex1bApp(
            ctx => ctx.ThemePanel(
                outer => outer
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
                ctx.VStack(v => [
                    v.Text("Outer theme - Cyan"),
                    v.ThemePanel(
                        inner => inner
                            .Set(GlobalTheme.ForegroundColor, Hex1bColor.Yellow),
                        v.VStack(innerV => [
                            innerV.Text("Inner theme - Yellow")
                        ])
                    ),
                    v.Text("Back to outer - Cyan")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Back to outer"), TimeSpan.FromSeconds(2))
            .Capture("themepanel-nested-doc-example")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Find the line indices for each text
        int? outerLine1 = null, innerLine = null, outerLine2 = null;
        for (int y = 0; y < snapshot.Height; y++)
        {
            var line = snapshot.GetLine(y);
            if (line.Contains("Outer theme - Cyan") && outerLine1 is null)
                outerLine1 = y;
            else if (line.Contains("Inner theme - Yellow"))
                innerLine = y;
            else if (line.Contains("Back to outer - Cyan"))
                outerLine2 = y;
        }

        Assert.NotNull(outerLine1);
        Assert.NotNull(innerLine);
        Assert.NotNull(outerLine2);

        // Check the foreground colors on the first character of each text line
        // Find where "Outer" starts on each line
        var line1 = snapshot.GetLine(outerLine1.Value);
        var col1 = line1.IndexOf("Outer");
        var cell1 = snapshot.GetCell(col1, outerLine1.Value);
        Assert.True(cell1.Foreground is not null && cell1.Foreground.Value.R == 0 && cell1.Foreground.Value.G == 255 && cell1.Foreground.Value.B == 255,
            $"First 'Outer theme - Cyan' should have cyan foreground, got {cell1.Foreground}");

        var lineInner = snapshot.GetLine(innerLine.Value);
        var colInner = lineInner.IndexOf("Inner");
        var cellInner = snapshot.GetCell(colInner, innerLine.Value);
        Assert.True(cellInner.Foreground is not null && cellInner.Foreground.Value.R == 255 && cellInner.Foreground.Value.G == 255 && cellInner.Foreground.Value.B == 0,
            $"'Inner theme - Yellow' should have yellow foreground, got {cellInner.Foreground}");

        var line2 = snapshot.GetLine(outerLine2.Value);
        var col2 = line2.IndexOf("Back");
        var cell2 = snapshot.GetCell(col2, outerLine2.Value);
        Assert.True(cell2.Foreground is not null && cell2.Foreground.Value.R == 0 && cell2.Foreground.Value.G == 255 && cell2.Foreground.Value.B == 255,
            $"'Back to outer - Cyan' should have cyan foreground, got {cell2.Foreground}");
    }

    /// <summary>
    /// Verifies nested ThemePanels can combine themes (cumulative inheritance).
    /// This matches themepanel-nesting.cs snippet: outer sets foreground cyan, inner adds background.
    /// </summary>
    [Fact]
    public async Task ThemePanel_NestedDocExample_CumulativeThemes()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 45, 8);

        using var app = new Hex1bApp(
            ctx => ctx.ThemePanel(
                outer => outer
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
                ctx.VStack(v => [
                    v.Text("Outer theme applies here"),
                    v.ThemePanel(
                        inner => inner
                            .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(0, 0, 139)),
                        v.Text("Both themes combined")
                    ),
                    v.Text("Only outer theme here")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Only outer theme here"), TimeSpan.FromSeconds(2))
            .Capture("themepanel-nesting-doc-example")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Find the line indices for each text
        int? outerLine1 = null, combinedLine = null, outerLine2 = null;
        for (int y = 0; y < snapshot.Height; y++)
        {
            var line = snapshot.GetLine(y);
            if (line.Contains("Outer theme applies here"))
                outerLine1 = y;
            else if (line.Contains("Both themes combined"))
                combinedLine = y;
            else if (line.Contains("Only outer theme here"))
                outerLine2 = y;
        }

        Assert.NotNull(outerLine1);
        Assert.NotNull(combinedLine);
        Assert.NotNull(outerLine2);

        // Check colors on the text cells
        var line1 = snapshot.GetLine(outerLine1.Value);
        var col1 = line1.IndexOf("Outer");
        var cell1 = snapshot.GetCell(col1, outerLine1.Value);
        Assert.True(cell1.Foreground is not null && cell1.Foreground.Value.R == 0 && cell1.Foreground.Value.G == 255 && cell1.Foreground.Value.B == 255,
            $"First outer row should have cyan foreground, got {cell1.Foreground}");

        var lineCombined = snapshot.GetLine(combinedLine.Value);
        var colCombined = lineCombined.IndexOf("Both");
        var cellCombined = snapshot.GetCell(colCombined, combinedLine.Value);
        // Combined row should have cyan foreground inherited from outer
        Assert.True(cellCombined.Foreground is not null && cellCombined.Foreground.Value.R == 0 && cellCombined.Foreground.Value.G == 255 && cellCombined.Foreground.Value.B == 255,
            $"Combined row should inherit cyan foreground from outer, got {cellCombined.Foreground}");
        // Combined row should have dark blue background from inner
        Assert.True(cellCombined.Background is not null && cellCombined.Background.Value.R == 0 && cellCombined.Background.Value.G == 0 && cellCombined.Background.Value.B == 139,
            $"Combined row should have dark blue background from inner ThemePanel, got {cellCombined.Background}");

        var line2 = snapshot.GetLine(outerLine2.Value);
        var col2 = line2.IndexOf("Only");
        var cell2 = snapshot.GetCell(col2, outerLine2.Value);
        Assert.True(cell2.Foreground is not null && cell2.Foreground.Value.R == 0 && cell2.Foreground.Value.G == 255 && cell2.Foreground.Value.B == 255,
            $"Second outer row should have cyan foreground, got {cell2.Foreground}");
    }

    #endregion

    #region Container Widget Tests

    /// <summary>
    /// Verifies ThemePanel works correctly inside VStack.
    /// </summary>
    [Fact]
    public async Task ThemePanel_InsideVStack_AppliesTheme()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 12);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("VStack Container Test"),
                v.ThemePanel(
                    theme => theme
                        .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(128, 0, 128))
                        .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.FromRgb(255, 255, 255)),
                    ctx.Button("Purple Button")
                ),
                v.Button("Default Button")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Default Button"), TimeSpan.FromSeconds(2))
            .Capture("themepanel-in-vstack")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(128, 0, 128)),
            "ThemePanel button should have purple background");
    }

    /// <summary>
    /// Verifies ThemePanel works correctly inside HStack.
    /// </summary>
    [Fact]
    public async Task ThemePanel_InsideHStack_AppliesTheme()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 10);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("HStack Container Test"),
                v.HStack(h => [
                    h.Button("Default Left"),
                    h.ThemePanel(
                        theme => theme
                            .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(0, 128, 0)),
                        ctx.Button("Green Middle")
                    ),
                    h.Button("Default Right")
                ])
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Green Middle"), TimeSpan.FromSeconds(2))
            .Capture("themepanel-in-hstack")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(0, 128, 0)),
            "ThemePanel button should have green background");
    }

    /// <summary>
    /// Verifies ThemePanel works correctly inside Border.
    /// </summary>
    [Fact]
    public async Task ThemePanel_InsideBorder_AppliesTheme()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 12);

        using var app = new Hex1bApp(
            ctx => ctx.Border(
                ctx.ThemePanel(
                    theme => theme
                        .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(255, 128, 0))
                        .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.FromRgb(0, 0, 0)),
                    ctx.VStack(inner => [
                        inner.Text("Inside Border"),
                        inner.Button("Orange Button")
                    ])
                ),
                title: "Themed Content"
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Orange Button"), TimeSpan.FromSeconds(2))
            .Capture("themepanel-in-border")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(255, 128, 0)),
            "ThemePanel button inside border should have orange background");
    }

    /// <summary>
    /// Verifies ThemePanel works correctly inside VScroll container.
    /// </summary>
    [Fact]
    public async Task ThemePanel_InsideVScroll_AppliesTheme()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 15);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Scroll Container Test"),
                v.VScroll(
                    ctx.ThemePanel(
                        theme => theme
                            .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(0, 200, 200)),
                        ctx.VStack(inner => [
                            inner.Button("Cyan Button 1"),
                            inner.Button("Cyan Button 2"),
                            inner.Button("Cyan Button 3")
                        ])
                    )
                ).FixedHeight(5)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Cyan Button"), TimeSpan.FromSeconds(2))
            .Capture("themepanel-in-vscroll")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(0, 200, 200)),
            "ThemePanel buttons inside scroll should have cyan background");
    }

    /// <summary>
    /// Verifies ThemePanel works correctly inside Splitter.
    /// </summary>
    [Fact]
    public async Task ThemePanel_InsideSplitter_AppliesTheme()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 15);

        using var app = new Hex1bApp(
            ctx => ctx.HSplitter(
                ctx.ThemePanel(
                    theme => theme
                        .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(200, 0, 200))
                        .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.FromRgb(255, 255, 255)),
                    ctx.VStack(left => [
                        left.Text("Left Pane"),
                        left.Button("Magenta Left")
                    ])
                ),
                ctx.ThemePanel(
                    theme => theme
                        .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(200, 200, 0)),
                    ctx.VStack(right => [
                        right.Text("Right Pane"),
                        right.Button("Yellow Right")
                    ])
                ),
                leftWidth: 30
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Yellow Right"), TimeSpan.FromSeconds(2))
            .Capture("themepanel-in-splitter")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // The left button is focused, so it should have magenta FocusedBackgroundColor
        Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(200, 0, 200)),
            "Left pane button should have magenta focused background");
        // Right pane button is NOT focused, so we can't assert on BackgroundColor
        // as unfocused buttons don't show background by default
    }

    #endregion

    #region Theme Caching Tests

    /// <summary>
    /// Verifies that a cached theme can be used with ThemePanel.
    /// </summary>
    [Fact]
    public async Task ThemePanel_WithCachedTheme_WorksCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 12);

        // Pre-create a cached theme
        var cachedTheme = new Hex1bTheme("CachedBlue")
            .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(30, 60, 120))
            .Set(ButtonTheme.ForegroundColor, Hex1bColor.FromRgb(255, 255, 255));

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Cached Theme Test"),
                v.ThemePanel(
                    _ => cachedTheme, // Return cached theme, ignore input
                    ctx.VStack(inner => [
                        inner.Button("Cached Theme Button 1"),
                        inner.Button("Cached Theme Button 2")
                    ])
                )
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Cached Theme Button 2"), TimeSpan.FromSeconds(2))
            .Capture("themepanel-cached-theme")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(30, 60, 120)),
            "Buttons should use cached theme's blue background");
    }

    /// <summary>
    /// Verifies that passing through the theme unchanged has no visual effect.
    /// </summary>
    /// <remarks>
    /// This test is currently flaky when run with other tests due to what appears
    /// to be a test isolation issue. It passes reliably when run in isolation.
    /// </remarks>
    [Fact]
    public async Task ThemePanel_PassthroughTheme_HasNoEffect()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Passthrough Theme Test"),
                v.ThemePanel(
                    theme => theme, // Just return the theme unchanged
                    ctx.Button("Should Look Normal")
                )
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Should Look Normal"), TimeSpan.FromSeconds(2))
            .Capture("themepanel-passthrough")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Default focused button is white background
        Assert.True(snapshot.HasBackgroundColor(Hex1bColor.White),
            "Button with passthrough theme should use default styling");
    }

    #endregion

    #region Multiple Widget Types in Single ThemePanel

    /// <summary>
    /// Verifies that multiple widget types inside a ThemePanel all respect the theme.
    /// </summary>
    [Fact]
    public async Task ThemePanel_MultipleWidgetTypes_AllThemed()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 70, 20);
        IReadOnlyList<string> items = ["Item A", "Item B"];

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Multiple Widget Types Theme Test"),
                v.Text(""),
                v.ThemePanel(
                    theme => theme
                        .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(100, 0, 0))
                        .Set(TextBoxTheme.CursorBackgroundColor, Hex1bColor.FromRgb(0, 100, 0))
                        .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.FromRgb(0, 0, 100))
                        .Set(BorderTheme.BorderColor, Hex1bColor.FromRgb(255, 128, 0)),
                    ctx.Border(
                        ctx.VStack(inner => [
                            inner.Text("All widgets in this panel are themed:"),
                            inner.Button("Red Background Button"),
                            inner.TextBox("Green Cursor TextBox"),
                            inner.List(items)
                        ]),
                        title: "Orange Border"
                    )
                )
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item B"), TimeSpan.FromSeconds(2))
            .Capture("themepanel-multiple-widgets")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Check that multiple themed colors are present
        Assert.True(snapshot.HasForegroundColor(Hex1bColor.FromRgb(255, 128, 0)),
            "Border should have orange color");
        Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(0, 0, 100)),
            "Selected list item should have blue background");
    }

    #endregion

    #region Terminal Width Responsiveness

    /// <summary>
    /// Verifies ThemePanel works correctly at various terminal widths.
    /// </summary>
    [Theory]
    [InlineData(40)]
    [InlineData(60)]
    [InlineData(80)]
    [InlineData(120)]
    public async Task ThemePanel_RespondsToTerminalWidth(int width)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, width, 10);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text($"Width: {width} columns"),
                v.ThemePanel(
                    theme => theme
                        .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(0, 128, 255))
                        .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.FromRgb(255, 255, 255)),
                    ctx.Button("Responsive Blue Button")
                )
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Responsive"), TimeSpan.FromSeconds(2))
            .Capture($"themepanel-width-{width}")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(0, 128, 255)),
            $"ThemePanel button should have blue background at width {width}");
    }

    #endregion

    #region Asciinema Recording Tests

    /// <summary>
    /// Records an asciinema demonstration of ThemePanel scope boundaries.
    /// </summary>
    [Fact]
    public async Task ThemePanel_RecordsScopeBoundaryDemo()
    {
        var tempFile = GetTempFile();
        using var workload = new Hex1bAppWorkloadAdapter();
        var terminalOptions = new Hex1bTerminalOptions
        {
            Width = 70,
            Height = 18,
            WorkloadAdapter = workload
        };
        var recorder = terminalOptions.AddAsciinemaRecorder(tempFile, new AsciinemaRecorderOptions
        {
            Title = "ThemePanel Scope Boundary Demo",
            IdleTimeLimit = 0.5f
        });
        using var terminal = new Hex1bTerminal(terminalOptions);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("═══════════════════════════════════════════════════════"),
                v.Text("          ThemePanel Theme Scoping Demo                 "),
                v.Text("═══════════════════════════════════════════════════════"),
                v.Text(""),
                v.Button("Default Button (White focus)"),
                v.Text(""),
                v.Border(
                    ctx.ThemePanel(
                        theme => theme
                            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(0, 100, 200))
                            .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.FromRgb(255, 255, 255)),
                        ctx.VStack(inner => [
                            inner.Button("Themed Button 1 (Blue focus)"),
                            inner.Button("Themed Button 2 (Blue focus)")
                        ])
                    ),
                    title: "ThemePanel Boundary"
                ),
                v.Text(""),
                v.Button("Another Default Button")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        recorder.AddMarker("Initial State");

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Another Default"), TimeSpan.FromSeconds(2))
            .Wait(TimeSpan.FromMilliseconds(500))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        recorder.AddMarker("Navigate to Themed Buttons");

        // Navigate through buttons to show theme scoping
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .Wait(TimeSpan.FromMilliseconds(300))
            .Key(Hex1bKey.Tab)
            .Wait(TimeSpan.FromMilliseconds(300))
            .Key(Hex1bKey.Tab)
            .Wait(TimeSpan.FromMilliseconds(300))
            .Key(Hex1bKey.Tab)
            .Wait(TimeSpan.FromMilliseconds(500))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        recorder.AddMarker("Back to Default");

        var snapshot = terminal.CreateSnapshot();
        TestCaptureHelper.Capture(snapshot, "themepanel-scope-demo");
        await TestCaptureHelper.CaptureCastAsync(recorder, "themepanel-scope-boundary", TestContext.Current.CancellationToken);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Records an asciinema demonstration of nested ThemePanels.
    /// </summary>
    [Fact]
    public async Task ThemePanel_RecordsNestedPanelDemo()
    {
        var tempFile = GetTempFile();
        using var workload = new Hex1bAppWorkloadAdapter();
        var terminalOptions = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 20,
            WorkloadAdapter = workload
        };
        var recorder = terminalOptions.AddAsciinemaRecorder(tempFile, new AsciinemaRecorderOptions
        {
            Title = "Nested ThemePanel Demo",
            IdleTimeLimit = 0.5f
        });
        using var terminal = new Hex1bTerminal(terminalOptions);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("╔══════════════════════════════════════════════════════════════╗"),
                v.Text("║              Nested ThemePanel Demonstration                 ║"),
                v.Text("╚══════════════════════════════════════════════════════════════╝"),
                v.Text(""),
                v.ThemePanel(
                    theme => theme
                        .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(0, 0, 200)),
                    ctx.Border(
                        ctx.VStack(outer => [
                            outer.Text("Outer ThemePanel: Blue buttons"),
                            outer.Button("Blue Outer 1"),
                            outer.ThemePanel(
                                innerTheme => innerTheme
                                    .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(200, 0, 0)),
                                ctx.Border(
                                    ctx.VStack(inner => [
                                        inner.Text("Inner ThemePanel: Red buttons"),
                                        inner.Button("Red Inner 1"),
                                        inner.Button("Red Inner 2")
                                    ]),
                                    title: "Nested Panel"
                                )
                            ),
                            outer.Button("Blue Outer 2")
                        ]),
                        title: "Outer Panel"
                    )
                )
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        recorder.AddMarker("Initial State");

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Blue Outer 2"), TimeSpan.FromSeconds(2))
            .Wait(TimeSpan.FromMilliseconds(500))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        recorder.AddMarker("Navigate Through Nested Structure");

        // Navigate through all buttons
        for (int i = 0; i < 4; i++)
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .Key(Hex1bKey.Tab)
                .Wait(TimeSpan.FromMilliseconds(400))
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        }

        recorder.AddMarker("Completed Navigation");

        var snapshot = terminal.CreateSnapshot();
        TestCaptureHelper.Capture(snapshot, "themepanel-nested-demo");
        await TestCaptureHelper.CaptureCastAsync(recorder, "themepanel-nested", TestContext.Current.CancellationToken);

        cts.Cancel();
        await runTask;
    }

    #endregion

    #region Complex Real-World Scenarios

    /// <summary>
    /// Demonstrates a realistic dashboard with themed sections.
    /// </summary>
    [Fact]
    public async Task ThemePanel_Dashboard_WithThemedSections()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 100, 25);
        IReadOnlyList<string> menuItems = ["Dashboard", "Settings", "Help"];

        using var app = new Hex1bApp(
            ctx => ctx.HSplitter(
                // Left sidebar - dark theme
                ctx.ThemePanel(
                    theme => theme
                        .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.FromRgb(50, 50, 80))
                        .Set(ListTheme.SelectedForegroundColor, Hex1bColor.FromRgb(200, 200, 255)),
                    ctx.VStack(sidebar => [
                        sidebar.Text("Navigation"),
                        sidebar.Text("──────────"),
                        sidebar.List(menuItems)
                    ])
                ),
                // Main content area
                ctx.VStack(main => [
                    // Header - accent theme
                    main.ThemePanel(
                        theme => theme
                            .Set(BorderTheme.BorderColor, Hex1bColor.FromRgb(100, 200, 100))
                            .Set(BorderTheme.TitleColor, Hex1bColor.FromRgb(150, 255, 150)),
                        ctx.Border(ctx.Text("System Dashboard - All Systems Operational"), title: "Status")
                    ),
                    main.Text(""),
                    // Action buttons - primary theme
                    main.ThemePanel(
                        theme => theme
                            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(0, 150, 0))
                            .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.FromRgb(255, 255, 255)),
                        ctx.HStack(actions => [
                            actions.Button("Refresh"),
                            actions.Text(" "),
                            actions.Button("Export"),
                            actions.Text(" "),
                            actions.Button("Settings")
                        ])
                    ),
                    main.Text(""),
                    // Progress section
                    main.ThemePanel(
                        theme => theme
                            .Set(ProgressTheme.FilledForegroundColor, Hex1bColor.FromRgb(0, 200, 255)),
                        ctx.VStack(progress => [
                            progress.Text("CPU Usage:"),
                            progress.Progress(65),
                            progress.Text("Memory:"),
                            progress.Progress(42)
                        ])
                    )
                ]),
                leftWidth: 20
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Memory:"), TimeSpan.FromSeconds(2))
            .Capture("themepanel-dashboard")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Verify multiple themed sections
        Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(50, 50, 80)),
            "Sidebar list should have dark purple selection");
        Assert.True(snapshot.HasForegroundColor(Hex1bColor.FromRgb(100, 200, 100)),
            "Header border should have green color");
    }

    /// <summary>
    /// Demonstrates a form with validation-themed fields.
    /// </summary>
    [Fact]
    public async Task ThemePanel_Form_WithValidationThemes()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 70, 20);

        using var app = new Hex1bApp(
            ctx => ctx.Border(
                ctx.VStack(v => [
                    v.Text("User Registration Form"),
                    v.Text("══════════════════════"),
                    v.Text(""),
                    // Valid field - green theme
                    v.HStack(row => [
                        row.Text("Username: "),
                        row.ThemePanel(
                            theme => theme
                                .Set(TextBoxTheme.CursorBackgroundColor, Hex1bColor.FromRgb(0, 200, 0)),
                            ctx.TextBox("valid_user")
                        ),
                        row.Text(" ✓")
                    ]),
                    v.Text(""),
                    // Error field - red theme  
                    v.HStack(row => [
                        row.Text("Email:    "),
                        row.ThemePanel(
                            theme => theme
                                .Set(TextBoxTheme.CursorBackgroundColor, Hex1bColor.FromRgb(200, 0, 0)),
                            ctx.TextBox("invalid-email")
                        ),
                        row.Text(" ✗")
                    ]),
                    v.Text(""),
                    // Submit button - accent theme
                    v.ThemePanel(
                        theme => theme
                            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(0, 100, 200))
                            .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.FromRgb(255, 255, 255)),
                        ctx.Button("Submit Registration")
                    )
                ]),
                title: "Registration"
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Submit Registration"), TimeSpan.FromSeconds(2))
            .Capture("themepanel-form-validation")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(snapshot.ContainsText("valid_user"), "Form should contain valid username");
        Assert.True(snapshot.ContainsText("invalid-email"), "Form should contain invalid email");
    }

    #endregion
}
