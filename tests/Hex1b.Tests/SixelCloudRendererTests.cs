using System.Text;
using Hex1b.Sixel;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class SixelCloudRendererTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RenderFrame_MotesSharingCell_KeepIndependentCursorAnchors(bool cursorToRight)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 10, 20, 20, 8);
        terminal.ApplyTokens([new PrivateModeToken(8452, cursorToRight)]);
        var cloud = new DustCloud(1);
        cloud.Reset(200, 160, 2);
        cloud.Motes[0].X = 12;
        cloud.Motes[0].Y = 22;
        cloud.Motes[1].X = 14;
        cloud.Motes[1].Y = 24;
        var renderer = new SixelCloudRenderer(10, 20);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize(Encoding.ASCII.GetString(renderer.RenderFrame(cloud, 20, 8))));

        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(2, snapshot.SixelPlacements.Count);
        foreach (var placement in snapshot.SixelPlacements)
        {
            Assert.AreEqual(1, placement.Row);
            Assert.AreEqual(1, placement.Column);
        }
        var firstPixels = snapshot.SixelPlacements[0].Image.GetPixels();
        var secondPixels = snapshot.SixelPlacements[1].Image.GetPixels();
        Assert.IsNotNull(firstPixels);
        Assert.IsNotNull(secondPixels);
        Assert.AreEqual(255, firstPixels[2, 2].A);
        Assert.AreEqual(255, secondPixels[4, 4].A);
        Assert.AreEqual(0, terminal.ScrollbackCount);
    }

    [TestMethod]
    [DataRow(10d, 20d, 80, 24)]
    [DataRow(16d, 38d, 80, 24)]
    [DataRow(7.5d, 15.5d, 40, 12)]
    [DataRow(10d, 20d, 20, 3)]
    [DataRow(10d, 20d, 20, 2)]
    [DataRow(10d, 20d, 20, 1)]
    [DataRow(2d, 2d, 20, 5)]
    public void RenderFrame_BottomEdgePlacements_DoNotScrollEarlierMotes(
        double cellWidth, double cellHeight, int columns, int rows)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, cellWidth, cellHeight, columns, rows);
        var cloud = new DustCloud(1);
        cloud.Reset((int)(columns * cellWidth), (int)(rows * cellHeight), 2);
        cloud.Motes[0].X = 1;
        cloud.Motes[0].Y = 1;
        cloud.Motes[1].X = cellWidth + 1;
        cloud.Motes[1].Y = (rows - 2) * cellHeight + .5;
        var renderer = new SixelCloudRenderer(cellWidth, cellHeight);

        for (var frame = 0; frame < 3; frame++)
        {
            terminal.ApplyTokens(AnsiTokenizer.Tokenize(
                Encoding.ASCII.GetString(renderer.RenderFrame(cloud, columns, rows))));
            Assert.AreEqual(0, terminal.ScrollbackCount, $"Frame {frame} scrolled the viewport.");
            using var snapshot = terminal.CreateSnapshot();
            Assert.AreEqual(rows <= 2 ? 0 : 1, snapshot.SixelPlacements.Count);
            if (rows > 2)
            {
                Assert.AreEqual(0, TestSeq.Single(snapshot.SixelPlacements).Row);
            }
        }
    }

    [TestMethod]
    [DataRow(10d, 20d, 20, 24)]
    [DataRow(16d, 38d, 20, 12)]
    [DataRow(7.5d, 15.5d, 20, 12)]
    [DataRow(2d, 2d, 20, 2)]
    [DataRow(10d, 20d, 20, 1)]
    public void RenderFrame_RasterAtBottomBand_DoesNotScroll(
        double cellWidth, double cellHeight, int columns, int rows)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, cellWidth, cellHeight, columns, rows);
        var cloud = new DustCloud(1);
        cloud.Reset((int)(columns * cellWidth), (int)(rows * cellHeight), 2);
        cloud.Motes[0].X = 1;
        cloud.Motes[0].Y = 1;
        cloud.Motes[1].X = 1;
        cloud.Motes[1].Y = (int)((rows - 1) * cellHeight) - 2;
        var renderer = new SixelCloudRenderer(cellWidth, cellHeight, useRaster: true);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            Encoding.ASCII.GetString(renderer.RenderFrame(cloud, columns, rows))));

        Assert.AreEqual(0, terminal.ScrollbackCount);
        using var snapshot = terminal.CreateSnapshot();
        if ((rows - 1) * cellHeight < 6)
        {
            Assert.IsEmpty(snapshot.SixelPlacements);
        }
        else
        {
            var placement = TestSeq.Single(snapshot.SixelPlacements);
            Assert.AreEqual(0, placement.Row);
            var pixels = placement.Image.GetPixels();
            Assert.IsNotNull(pixels);
            Assert.AreEqual(255, pixels[1, 1].A);
        }
    }

    private static Hex1bTerminal CreateTerminal(
        Hex1bAppWorkloadAdapter workload, double cellWidth, double cellHeight, int columns, int rows)
        => Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(new TerminalCapabilities
            {
                SupportsSixel = true,
                SixelSupport = SixelPresentationSupport.Headless,
                CellPixelWidth = (int)Math.Ceiling(cellWidth),
                CellPixelHeight = (int)Math.Ceiling(cellHeight),
                SixelCellMetrics = new(cellWidth, cellHeight,
                    SixelCellMetricsSource.Direct, SixelCellMetricsReliability.Authoritative)
            })
            .WithDimensions(columns, rows)
            .WithScrollback(100)
            .Build();
}
