#pragma warning disable HEX1B_COMPOSITION

using Hex1b;
using Hex1b.Composition;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests.Composition;

/// <summary>
/// End-to-end tests for the slash-command prompt pattern: a composite that
/// turns a TextBox into a slash-command prompt with a completion palette.
/// Mirrors the structure used by AgenticPromptDemo's SlashCommandPromptWidget.
///
/// Note: the palette is rendered as a flow child above the textbox, not a
/// FloatWidget. A composite whose content is just one row tall (the textbox)
/// allocates a one-row child surface during compositing, and any FloatWidget
/// arranged outside those bounds is clipped. Putting the palette in flow makes
/// the composite grow vertically when visible, which is what users expect.
/// </summary>
public class SlashCommandIntegrationTests
{
    [Fact]
    public async Task TypingSlash_RendersPaletteAboveTextBox()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new SlashPromptHostWidget()),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Type("/")
            .WaitUntil(s => s.ContainsText("washthedishes"), TimeSpan.FromSeconds(5))
            .Capture("after-slash")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var screen = string.Join("\n", Enumerable.Range(0, 24).Select(r =>
        {
            try { return snapshot.GetTextAt(r, 0, 79); }
            catch { return ""; }
        }));

        Assert.True(snapshot.ContainsText("washthedishes"),
            $"Palette should appear when text starts with '/'\n\nScreen:\n{screen}");
        Assert.True(snapshot.ContainsText("picker"),
            $"All matching commands should appear\n\nScreen:\n{screen}");
        Assert.True(snapshot.ContainsText("Commands"),
            $"Border title should appear\n\nScreen:\n{screen}");
    }

    [Fact]
    public async Task TypingNonSlash_DoesNotRenderPalette()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new SlashPromptHostWidget()),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Type("hello")
            .WaitUntil(s => s.ContainsText("hello"), TimeSpan.FromSeconds(5))
            .Capture("non-slash")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.False(snapshot.ContainsText("Commands"),
            "Palette should NOT appear when text does not start with '/'");
        Assert.False(snapshot.ContainsText("washthedishes"),
            "No completions should be shown");
    }

    private sealed record SlashPromptHostWidget : Hex1bCompositeWidget
    {
        private static readonly IReadOnlyList<DemoCommand> Commands =
        [
            new("picker", "Show buttons"),
            new("clear", "Clear transcript"),
            new("washthedishes", "Pretend"),
        ];

        protected override Hex1bWidget Build(CompositionContext ctx)
        {
            return ctx.VStack(v =>
            [
                v.Text("--- transcript ---"),
                v.Text(""),
                v.Text(""),
                v.Text(""),
                v.Text(""),
                v.Separator(),
                v.SlashPrompt(Commands),
            ]);
        }
    }

    internal sealed record SlashPromptWidget(IReadOnlyList<DemoCommand> Commands) : Hex1bCompositeWidget
    {
        protected override Hex1bWidget Build(CompositionContext ctx)
        {
            var state = ctx.UseState(() => new PromptState());

            var matches = Commands.Where(c =>
                state.Text.StartsWith("/", StringComparison.Ordinal) &&
                c.Name.StartsWith(state.Text.Substring(1), StringComparison.OrdinalIgnoreCase))
                .ToList();
            var paletteVisible = matches.Count > 0;

            return ctx.VStack(v =>
            {
                var tb = v.TextBox()
                    .OnTextChanged(e => state.Text = e.NewText);

                if (paletteVisible)
                {
                    var palette = v.Border(b => matches
                        .Select((cmd, i) => (Hex1bWidget)b.Text("/" + cmd.Name + "  " + cmd.Description))
                        .ToArray()).Title("Commands");
                    return [palette, tb];
                }

                return [tb];
            });
        }
    }

    internal sealed record DemoCommand(string Name, string Description);

    private sealed class PromptState { public string Text = ""; }
}

internal static class SlashPromptExtensions
{
    public static SlashCommandIntegrationTests.SlashPromptWidget SlashPrompt<T>(
        this WidgetContext<T> _,
        IReadOnlyList<SlashCommandIntegrationTests.DemoCommand> commands)
        where T : Hex1bWidget
        => new(commands);
}
