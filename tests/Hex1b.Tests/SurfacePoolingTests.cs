using Hex1b.Input;
using Hex1b.Surfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Tests;

[TestClass]
public sealed class SurfacePoolingTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(null, false)]
    [DataRow(true, false)]
    [DataRow(false, false)]
    [DataRow(null, true)]
    [DataRow(true, true)]
    [DataRow(false, true)]
    public async Task Rendering_RuntimeOption_PreservesDefaultAndSurfaceReuse(bool? enabled, bool caching)
    {
        Assert.IsTrue(new Hex1bAppOptions().EnableSurfacePooling);
        var frame = 0;
        Surface? observedSurface = null;
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(options =>
            {
                if (enabled.HasValue)
                    options.EnableSurfacePooling = enabled.Value;
                options.EnableRenderCaching = caching;
                options.EnableRescue = false;
                options.EnableInputCoalescing = false;
            }, ctx => ctx.EffectPanel(ctx.Button($"frame:{frame}").OnClick(_ => frame++))
                .Effect(surface => Volatile.Write(ref observedSurface, surface)))
            .WithHeadless().WithDimensions(40, 5).Build();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(10));
        var run = terminal.RunAsync(cancellation.Token);
        try
        {
            using var initial = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("frame:0"), TimeSpan.FromSeconds(5), "initial effect")
                .Build().ApplyAsync(terminal, cancellation.Token);
            var first = Volatile.Read(ref observedSurface);
            Assert.IsNotNull(first);
            using var updated = await new Hex1bTerminalInputSequenceBuilder()
                .Key(Hex1bKey.Enter)
                .WaitUntil(s => s.ContainsText("frame:1"), TimeSpan.FromSeconds(5), "updated effect")
                .Build().ApplyAsync(terminal, cancellation.Token);
            var second = Volatile.Read(ref observedSurface);
            Assert.IsNotNull(second);
            Assert.AreEqual(enabled ?? true, ReferenceEquals(first, second));
        }
        finally
        {
            cancellation.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
