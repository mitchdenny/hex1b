#pragma warning disable HEX1B_COMPOSITION

using AgenticPromptDemo;
using Hex1b;
using Hex1b.Composition;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests.Composition;

/// <summary>
/// Drives the *real* SlashCommandPromptWidget from samples/AgenticPromptDemo
/// (linked into this test assembly) through the headless input pipeline so we
/// can reproduce the user-reported bug "Up/Down arrows don't change selection".
///
/// Mirrors the demo's surrounding tree (VStack with siblings + the prompt at
/// the bottom) so reconciliation positions match what the demo experiences.
/// </summary>
public class RealSlashCommandPromptWidgetTests
{
    private static readonly IReadOnlyList<SlashCommand> Commands =
    [
        new("picker",        "Reply with action buttons"),
        new("clear",         "Clear the transcript"),
        new("help",          "Show help"),
        new("washthedishes", "Pretend"),
    ];

    [Fact]
    public async Task RealComposite_DownArrow_MovesSelection()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new RealSlashHostWidget()),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Type("/")
            .WaitUntil(s => s.ContainsText("picker"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(150))
            .Key(Hex1bKey.DownArrow)
            .Wait(TimeSpan.FromMilliseconds(250))
            .Capture("after-down")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var screen = string.Join("\n", Enumerable.Range(0, 24).Select(r =>
        {
            try { return snapshot.GetTextAt(r, 0, 79); }
            catch { return ""; }
        }));

        // Real composite uses U+276F '❯' as the marker
        var lines = screen.Split('\n');
        var pickerLine = lines.FirstOrDefault(l => l.Contains("picker")) ?? "";
        var clearLine = lines.FirstOrDefault(l => l.Contains("clear")) ?? "";

        Assert.True(clearLine.Contains('\u276F'),
            $"After DownArrow, '❯' should mark the 'clear' row.\n\nScreen:\n{screen}");
        Assert.False(pickerLine.Contains('\u276F'),
            $"After DownArrow, '❯' should no longer mark the 'picker' row.\n\nScreen:\n{screen}");
    }

    /// <summary>
    /// Regression test for a focus-loss bug observed in AgenticPromptDemo:
    /// when the slash palette opens, VStack reconciliation by index recreates
    /// the textbox node (it shifts from idx 0 to idx 1). FocusRing.EnsureFocus
    /// would then snap focus to the FIRST focusable in the entire app — in
    /// this test, the dummy Button above the prompt — instead of the textbox.
    ///
    /// Hex1bCompositeWidget.ReconcileAsync now restores focus inside the
    /// composite's subtree after rebuild, so DownArrow keeps reaching the
    /// textbox's palette navigation binding.
    /// </summary>
    [Fact]
    public async Task RealComposite_WithFocusableAbove_DownArrowMovesSelection()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new FocusableAboveHostWidget()),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            // Tab past the Button to focus the textbox.
            .Key(Hex1bKey.Tab)
            .Wait(TimeSpan.FromMilliseconds(150))
            .Type("/")
            .WaitUntil(s => s.ContainsText("picker"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(150))
            .Key(Hex1bKey.DownArrow)
            .Wait(TimeSpan.FromMilliseconds(250))
            .Capture("after-down-with-button")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var screen = string.Join("\n", Enumerable.Range(0, 24).Select(r =>
        {
            try { return snapshot.GetTextAt(r, 0, 79); }
            catch { return ""; }
        }));

        var lines = screen.Split('\n');
        var pickerLine = lines.FirstOrDefault(l => l.Contains("picker")) ?? "";
        var clearLine = lines.FirstOrDefault(l => l.Contains("clear")) ?? "";

        Assert.True(clearLine.Contains('\u276F'),
            $"After DownArrow with focusable sibling above, '❯' should mark 'clear'.\n\nScreen:\n{screen}");
        Assert.False(pickerLine.Contains('\u276F'),
            $"After DownArrow with focusable sibling above, '❯' should not mark 'picker'.\n\nScreen:\n{screen}");
    }

    private sealed record FocusableAboveHostWidget : Hex1bCompositeWidget
    {
        protected override Hex1bWidget Build(CompositionContext ctx)
        {
            return ctx.VStack(v =>
            [
                v.Button("dummy"),
                v.Separator(),
                v.SlashCommandPrompt(Commands),
            ]);
        }
    }

    private sealed record RealSlashHostWidget : Hex1bCompositeWidget
    {
        protected override Hex1bWidget Build(CompositionContext ctx)
        {
            return ctx.VStack(v =>
            [
                v.Text("--- transcript line 1 ---"),
                v.Text("--- transcript line 2 ---"),
                v.Text(""),
                v.Separator(),
                v.SlashCommandPrompt(Commands),
            ]);
        }
    }
}
