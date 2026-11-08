using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Tests.NativeAot;

[TestClass]
[DoNotParallelize]
[TestCategory("NativeAot")]
[NativeAotEnabled]
public sealed class NativeAotSurfacePoolingTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(null)]
    [DataRow(true)]
    [DataRow(false)]
    public Task HeadlessRendering_PublishTimePoolingSwitch_PreservesSnapshotsAndControlsRetention(bool? supported)
        => ScenarioRunner.RunAsync(Scenario.HeadlessSurfacePooling(supported), Source, TestContext, TestContext.CancellationToken);

    // The source and assembly name are identical across publishes; only the
    // consumer's RuntimeHostConfigurationOption changes.
    private const string Source = """
        using Hex1b;
        using Hex1b.Automation;
        using Hex1b.Charts;
        using Hex1b.Input;
        using Hex1b.Surfaces;
        using Hex1b.Theming;

        const string feature = "Hex1b.SurfacePooling.IsSupported";
        var supported = !AppContext.TryGetSwitch(feature, out var enabled) || enabled;
        Console.WriteLine($"PUBLISH_POOLING_SUPPORTED:{supported}");
        // A runtime attempt cannot undo the compiler's disabled feature gate.
        AppContext.SetSwitch(feature, true);
        foreach (var caching in new[] { false, true })
        foreach (var runtimeOption in new bool?[] { null, true, false })
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var frame = 0;
            Surface? observedSurface = null;
            var color = Hex1bColor.FromRgb(10, 20, 30);
            await using var terminal = Hex1bTerminal.CreateBuilder()
                .WithHex1bApp(options =>
                {
                    if (!options.EnableSurfacePooling)
                        throw new InvalidOperationException("Pooling must still default to true.");
                    if (runtimeOption.HasValue)
                        options.EnableSurfacePooling = runtimeOption.Value;
                    options.EnableRenderCaching = caching;
                    options.EnableRescue = false;
                    options.EnableInputCoalescing = false;
                }, ctx => ctx.EffectPanel(ctx.VStack(v => [
                    v.Button($"frame:{frame}").OnClick(_ => frame++),
                    v.Text("headless text"),
                    v.BarChart(new ChartItem("Alpha", 4), new ChartItem("Beta", 2))
                        .Range(0, 4).ShowValues().FixedHeight(5),
                    v.Surface(layers => [
                        layers.Layer(surface => surface.WriteText(0, 0, $"layer:{frame}"))
                    ]).FixedHeight(1)
                ])).Effect(surface =>
                {
                    surface[0, 1] = surface[0, 1] with { Background = color };
                    Volatile.Write(ref observedSurface, surface);
                }))
                .WithHeadless().WithDimensions(40, 10).Build();
            var run = terminal.RunAsync(cancellation.Token);
            try
            {
                using var initial = await new Hex1bTerminalInputSequenceBuilder()
                    .WaitUntil(s => IsRendered(s, 0), TimeSpan.FromSeconds(5), "initial text, chart and layer")
                    .Build().ApplyAsync(terminal, cancellation.Token);
                var first = Volatile.Read(ref observedSurface)
                    ?? throw new InvalidOperationException("Effect did not receive a surface.");
                VerifySnapshot(initial, 0, color);
                using var updated = await new Hex1bTerminalInputSequenceBuilder()
                    .Key(Hex1bKey.Enter)
                    .WaitUntil(s => IsRendered(s, 1), TimeSpan.FromSeconds(5), "updated text, chart and layer")
                    .Build().ApplyAsync(terminal, cancellation.Token);
                var second = Volatile.Read(ref observedSurface)
                    ?? throw new InvalidOperationException("Updated effect did not receive a surface.");
                VerifySnapshot(updated, 1, color);
                var reused = ReferenceEquals(first, second);
                var expected = supported && (runtimeOption ?? true);
                if (reused != expected)
                    throw new InvalidOperationException($"Pooling reuse={reused}, expected={expected}, option={runtimeOption}, caching={caching}.");
                Console.WriteLine($"POOLING:option={runtimeOption?.ToString() ?? "default"};caching={caching};reused={reused}");
                for (var row = 0; row < updated.Height; row++)
                    Console.WriteLine(updated.GetTextAt(row, 0, updated.Width));
            }
            finally
            {
                cancellation.Cancel();
                await run.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        Console.WriteLine("NATIVE_AOT_OK:HeadlessSurfacePooling");

        static bool IsRendered(Hex1bTerminalSnapshot snapshot, int frame) =>
            snapshot.ContainsText($"frame:{frame}") && snapshot.ContainsText("headless text") &&
            snapshot.ContainsText("Alpha") && snapshot.ContainsText("Beta") &&
            snapshot.ContainsText("\u2588") && snapshot.ContainsText($"layer:{frame}");

        static void VerifySnapshot(Hex1bTerminalSnapshot snapshot, int frame, Hex1bColor color)
        {
            if (!IsRendered(snapshot, frame) || snapshot.Width != 40 || snapshot.Height != 10 ||
                snapshot.GetTextAt(2, 6, 32) != new string('\u2588', 26) ||
                snapshot.GetTextAt(5, 6, 19) != new string('\u2588', 13) ||
                !snapshot.HasBackgroundColor(color))
            {
                for (var row = 0; row < snapshot.Height; row++)
                    Console.WriteLine($"SNAPSHOT[{row}]:{snapshot.GetTextAt(row, 0, snapshot.Width)}");
                throw new InvalidOperationException("Text/chart geometry or effect snapshot was incorrect.");
            }
        }
        """;
}
