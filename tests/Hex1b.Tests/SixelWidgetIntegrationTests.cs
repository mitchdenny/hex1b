#pragma warning disable HEX1B_SIXEL // Testing experimental Sixel API

using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Sixel;
using Hex1b.Surfaces;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class SixelWidgetIntegrationTests
{
    [TestMethod]
    public async Task SurfacePipeline_ReplacementMovementAndRemoval_LeavesOnlyCurrentPixels()
    {
        var red = CreateSolidPixels(30, 40, Rgba32.FromRgb(255, 0, 0));
        var blue = CreateSolidPixels(30, 40, Rgba32.FromRgb(0, 80, 255));
        var phase = 0;
        var capabilities = new TerminalCapabilities
        {
            SupportsSixel = true,
            SixelSupport = SixelPresentationSupport.Headless,
            SixelCellMetrics = new SixelCellMetrics(
                10,
                20,
                SixelCellMetricsSource.Direct,
                SixelCellMetricsReliability.Authoritative),
            CellPixelWidth = 10,
            CellPixelHeight = 20
        };
        using var workload = new Hex1bAppWorkloadAdapter(capabilities);
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(capabilities)
            .WithDimensions(20, 6)
            .Build();
        using var app = new Hex1bApp(
            context => Task.FromResult<Hex1bWidget>(
                context.VStack(column =>
                [
                    column.Button($"Advance phase {phase}")
                        .OnClick(_ => phase = Math.Min(2, phase + 1)),
                    column.HStack(row =>
                    [
                        row.Text(new string(' ', phase == 1 ? 4 : 0)),
                        phase == 2
                            ? row.Text("image removed")
                            : row.Sixel(
                                    phase == 0 ? red : blue,
                                    fallback => fallback.Text("Sixel unavailable"))
                                .Width(3)
                                .Height(2)
                    ])
                ])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(
                snapshot => snapshot.ContainsText("Advance phase 0") && snapshot.ContainsSixelData(),
                TimeSpan.FromSeconds(5),
                "initial Sixel image")
            .Capture("sixel-widget-initial")
            .Key(Hex1bKey.Enter)
            .WaitUntil(
                snapshot => snapshot.ContainsText("Advance phase 1"),
                TimeSpan.FromSeconds(5),
                "replacement image moved")
            .Capture("sixel-widget-replaced-moved")
            .Key(Hex1bKey.Enter)
            .WaitUntil(
                snapshot => snapshot.ContainsText("image removed"),
                TimeSpan.FromSeconds(5),
                "Sixel image removed")
            .Capture("sixel-widget-removed")
            .Ctrl().Key(Hex1bKey.C)
            .Build();

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await sequence.ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var captures = sequence.Steps.OfType<CaptureStep>().ToArray();
        var initial = TestSeq.Single(GetVisiblePlacements(captures[0].CapturedSnapshot!));
        Assert.AreEqual(0, initial.Column);
        AssertDominantColor(initial.GetPaintedPixels()![0, 0], red: true);

        var moved = TestSeq.Single(GetVisiblePlacements(captures[1].CapturedSnapshot!));
        Assert.AreEqual(4, moved.Column);
        AssertDominantColor(moved.GetPaintedPixels()![0, 0], red: false);

        Assert.IsEmpty(GetVisiblePlacements(captures[2].CapturedSnapshot!));
    }

    private static IReadOnlyList<SixelPlacement> GetVisiblePlacements(Hex1bTerminalSnapshot snapshot)
        => snapshot.SixelPlacements
            .Where(static placement => ContainsOpaquePixel(placement.GetPaintedPixels()))
            .ToArray();

    private static bool ContainsOpaquePixel(SixelPixelBuffer? pixels)
    {
        if (pixels is null)
        {
            return false;
        }

        foreach (var pixel in pixels.AsSpan())
        {
            if (pixel.A != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void AssertDominantColor(Rgba32 pixel, bool red)
    {
        Assert.AreEqual((byte)255, pixel.A);
        if (red)
        {
            Assert.IsTrue(pixel.R > 200 && pixel.G < 20 && pixel.B < 20);
        }
        else
        {
            Assert.IsTrue(pixel.B > 200 && pixel.B > pixel.G && pixel.G > pixel.R);
        }
    }

    private static SixelPixelBuffer CreateSolidPixels(int width, int height, Rgba32 color)
    {
        var pixels = new SixelPixelBuffer(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[x, y] = color;
            }
        }

        return pixels;
    }
}
