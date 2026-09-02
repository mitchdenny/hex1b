using System.Text;
using Hex1b.Sixel;
using Hex1b.Tokens;
using Hex1b.Surfaces;

namespace Hex1b.Tests.Sixel;

/// <summary>
/// Terminal-level integration tests for the Sixel raster environment: captured
/// background, terminal-scoped palette lifetime, and native passthrough.
/// </summary>
[TestClass]
public class SixelRasterIntegrationTests
{
    private const string DefineRed = "#1;2;100;0;0";

    [TestMethod]
    public async Task CapturedBackground_UsesTheBackgroundAtCreationTime()
    {
        await using var terminal = SixelTestTerminal.Create();

        // Blue background for the first graphic, green for the second.
        await FeedAsync(
            terminal,
            "\x1b[48;2;0;0;255m",
            $"\x1bP0;0q{DefineRed}#1@\x1b\\",
            "\x1b[2;1H",
            "\x1b[48;2;0;255;0m",
            "\x1bP0;0q#1@\x1b\\");
        await terminal.WaitForAsync(
            snapshot => snapshot.GetCell(0, 1).SixelData is not null,
            "two backgrounds",
            TestContext.Current.CancellationToken);

        var placements = terminal.Observe().Placements;
        Assert.AreEqual(2, placements.Count);
        Assert.Contains("#0000FFFF", placements[0].PixelGrid);
        Assert.DoesNotContain("#00FF00FF", placements[0].PixelGrid);
        Assert.Contains("#00FF00FF", placements[1].PixelGrid);
    }

    [TestMethod]
    public async Task CapturedBackground_DefaultsToBlackWhenUnset()
    {
        await using var terminal = SixelTestTerminal.Create();

        await FeedAsync(terminal, $"\x1bP0;0q{DefineRed}#1@\x1b\\");
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "default background",
            TestContext.Current.CancellationToken);

        var grid = TestSeq.Single(terminal.Observe().Placements).PixelGrid;
        Assert.Contains("#000000FF", grid);
    }

    [TestMethod]
    public async Task ColorRegisters_PersistAcrossAlternateScreenTransitions()
    {
        await using var terminal = SixelTestTerminal.Create();

        await FeedAsync(
            terminal,
            $"\x1bP0;1q{DefineRed}#1@\x1b\\",
            "\x1b[?1049h",
            "\x1bP0;1q#1@\x1b\\");
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "alternate screen palette",
            TestContext.Current.CancellationToken);

        var grid = TestSeq.Single(terminal.Observe().Placements).PixelGrid;
        Assert.Contains("#FF0000FF", grid);
    }

    [TestMethod]
    public async Task ColorRegisters_SurviveASoftReset()
    {
        await using var terminal = SixelTestTerminal.Create();

        await FeedAsync(
            terminal,
            $"\x1bP0;1q{DefineRed}#1@\x1b\\",
            "\x1b[!p",
            "\x1b[3;1H",
            "\x1bP0;1q#1@\x1b\\");
        await terminal.WaitForAsync(
            snapshot => snapshot.GetCell(0, 2).SixelData is not null,
            "soft reset palette",
            TestContext.Current.CancellationToken);

        var placements = terminal.Observe().Placements;
        Assert.Contains("#FF0000FF", placements[^1].PixelGrid);
    }

    [TestMethod]
    public async Task ColorRegisters_AreResetByRis()
    {
        await using var terminal = SixelTestTerminal.Create();

        await FeedAsync(terminal, $"\x1bP0;1q{DefineRed}#1@\x1b\\");
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "graphic before RIS",
            TestContext.Current.CancellationToken);

        // The raw byte path does not yet decode ESC c into a RIS token (owned by
        // #453), so drive the reset through the token stream directly.
        await terminal.FeedPreTokenizedAsync(
            Encoding.Latin1.GetBytes("\x1bc"),
            [RisToken.Instance],
            TestContext.Current.CancellationToken);
        await FeedAsync(terminal, "\x1bP0;1q#1@\x1b\\");
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "graphic after RIS",
            TestContext.Current.CancellationToken);

        var grid = TestSeq.Single(terminal.Observe().Placements).PixelGrid;
        Assert.DoesNotContain("#FF0000FF", grid);
        Assert.Contains(FormatColor(SixelDefaultPalette.Get(1)), grid);
    }

    [TestMethod]
    public async Task NativePassthrough_IsPreservedForRasterizedGraphics()
    {
        var payload = $"\x1bP0;0q\"1;1;2;2{DefineRed}#1@\x1b\\";
        await using var terminal = SixelTestTerminal.Create();

        await FeedAsync(terminal, payload);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "native passthrough",
            TestContext.Current.CancellationToken);
        await terminal.CompleteWorkloadAsync(TestContext.Current.CancellationToken);

        var presented = Encoding.Latin1.GetString(terminal.PresentationBytes);
        Assert.Contains(payload, presented);
    }

    [TestMethod]
    public async Task TerminalRaster_MatchesDirectRasterizationOfTheSamePayload()
    {
        var payload = $"\x1bP0;0q\"1;1;4;7{DefineRed}#2;2;0;100;0#1!3~$#2A\x1b\\";
        await using var terminal = SixelTestTerminal.Create();

        await FeedAsync(terminal, payload);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "parser and raster equivalence",
            TestContext.Current.CancellationToken);

        var expected = SixelRasterizer.Rasterize(
            SixelParser.ParsePayload(payload),
            SixelRasterEnvironment.CreateDefault());
        var expectedGrid = SixelPixelGrid.Format(expected.Image!.Materialize());
        var actualGrid = TestSeq.Single(terminal.Observe().Placements).PixelGrid;

        Assert.AreEqual(expectedGrid, actualGrid);
    }

    [TestMethod]
    public async Task GeometryOnlyGraphic_StillOccupiesCellsWithoutPixels()
    {
        await using var terminal = SixelTestTerminal.Create();

        await FeedAsync(terminal, "\x1bP0;1q\"1;1;999999999;999999999#1@\x1b\\");
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "geometry only",
            TestContext.Current.CancellationToken);

        var observation = terminal.Observe();
        var placement = TestSeq.Single(observation.Placements);
        Assert.AreEqual(0, placement.PixelWidth);
        Assert.AreEqual("", placement.PixelGrid);
        Assert.IsTrue(placement.WidthInCells > 0);
        Assert.IsTrue(observation.OccupiedCells.Count > 0);

        using var snapshot = terminal.Terminal.CreateSnapshot();
        var data = snapshot.GetCell(0, 0).SixelData;
        Assert.IsNotNull(data);
        Assert.IsNull(data.GetPixels());
        Assert.AreEqual(SixelRasterStatus.GeometryOnly, data.Raster.Status);
    }

    private static Task FeedAsync(SixelTestTerminal terminal, params string[] chunks) =>
        terminal.FeedAsync(Encoding.Latin1.GetBytes(string.Concat(chunks)));

    private static string FormatColor(Rgba32 color) =>
        $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
}
