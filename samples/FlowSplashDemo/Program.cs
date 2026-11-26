using System.Diagnostics;
using FlowSplashDemo;
using Hex1b;
using Hex1b.Input;

// Run with: dotnet run --project samples/FlowSplashDemo
// Resize during formation; the final frame should remain above the shell prompt.
using var stopping = new CancellationTokenSource();
var splash = new SplashAnimation();
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithDimensions(Math.Max(1, Console.WindowWidth), Math.Max(1, Console.WindowHeight))
    .WithHex1bFlow(async flow =>
    {
        var start = Stopwatch.GetTimestamp();
        var step = flow.Step(ctx => splash.Build(ctx, Stopwatch.GetElapsedTime(start).TotalMilliseconds)
            .InputBindings(bindings => bindings.Ctrl().Key(Hex1bKey.C).Action(_ => stopping.Cancel())));
        try
        {
            while (Stopwatch.GetElapsedTime(start).TotalMilliseconds < SplashAnimation.TotalDurationMs)
            {
                await Task.Delay(16, stopping.Token);
                step.Invalidate();
            }

            await step.CompleteAsync(ctx => ctx.VStack(_ => [
                splash.Build(ctx, SplashAnimation.TotalDurationMs)
                    .FixedWidth(flow.TerminalWidth)
                    .FixedHeight(Math.Max(1, flow.TerminalHeight - 1))
            ]), stopping.Token);
        }
        finally
        {
            step.Complete();
            await step.WaitForCompletionAsync();
        }
    }, options =>
    {
        options.UseSoftWrapTombstones = true;
        options.CursorRowProvider = () => null;
    })
    .Build();

try
{
    await terminal.RunAsync(stopping.Token);
}
catch (OperationCanceledException) when (stopping.IsCancellationRequested)
{
}
