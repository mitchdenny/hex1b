using Hex1b;
using Hex1b.Input;
using Hex1b.Integrations.Spectre.SpectreTui;
using Spectre.Tui;
using Spectre.Tui.App;

namespace Hex1b.Integrations.Spectre.Tests;

public class WithSpectreTuiAppBuilderTests
{
    [Fact]
    public void WithSpectreTuiApp_NullScreen_Throws()
    {
        var builder = Hex1bTerminal.CreateBuilder();
        Assert.Throws<ArgumentNullException>(
            () => builder.WithSpectreTuiApp(null!));
    }

    [Fact]
    public void WithSpectreTuiApp_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => ((Hex1bTerminalBuilder)null!).WithSpectreTuiApp(new QuitOnAttachScreen()));
    }

    [Fact]
    public void WithSpectreTuiTerminal_NullDelegate_Throws()
    {
        var builder = Hex1bTerminal.CreateBuilder();
        Assert.Throws<ArgumentNullException>(
            () => builder.WithSpectreTuiTerminal(null!));
    }

    [Fact]
    public async Task WithSpectreTuiTerminal_RunsToCompletion_AndForwardsOutput()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(40, 10)
            .WithHeadless()
            .WithSpectreTuiTerminal(async (tuiTerminal, ct) =>
            {
                tuiTerminal.MoveTo(0, 0);
                tuiTerminal.Write(new Cell().SetSymbol('h'));
                tuiTerminal.Write(new Cell().SetSymbol('i'));
                tuiTerminal.Flush();
                await Task.CompletedTask;
            }, mode: new InlineMode(3))
            .Build();

        await terminal.RunAsync().WaitAsync(TimeSpan.FromSeconds(5));

        var screenText = terminal.CreateSnapshot().GetScreenText();
        Assert.Contains("hi", screenText);
    }

    [Fact]
    public async Task WithSpectreTuiApp_RunsScreen_UntilQuit()
    {
        // QuitOnAttachScreen schedules context.Quit() shortly after the
        // first render, so the application loop terminates cleanly. Using
        // InlineMode here means the rendered content lands on the main
        // buffer where the Hex1b snapshot can read it; FullscreenMode
        // would draw to the alt screen and exit before snapshotting.
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(40, 10)
            .WithHeadless()
            .WithSpectreTuiApp(new QuitOnAttachScreen(), mode: new InlineMode(3))
            .Build();

        await terminal.RunAsync().WaitAsync(TimeSpan.FromSeconds(10));

        var screenText = terminal.CreateSnapshot().GetScreenText();
        Assert.Contains("X", screenText);
    }

    private sealed class QuitOnAttachScreen : Screen
    {
        public override void OnEnter(ApplicationContext context)
        {
            // Mark the context for shutdown after a tick so we still get
            // at least one render frame on the wire.
            _ = Task.Run(async () =>
            {
                await Task.Delay(50);
                context.Quit();
            });
        }

        public override void Render(RenderContext context)
        {
            context.SetSymbol(0, 0, 'X');
        }
    }
}
