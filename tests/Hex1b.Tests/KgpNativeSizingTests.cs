using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class KgpNativeSizingTests
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Display_NativeSize_UsesPixelOccupancyForCursorMovement(bool transmitAndDisplay)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        var pixels = KgpTestHelper.CreatePixelData(25, 45);
        var command = transmitAndDisplay
            ? KgpTestHelper.BuildCommand("a=T,f=32,s=25,v=45,i=1,X=9,Y=19,q=2", pixels)
            : KgpTestHelper.BuildCommand("a=t,f=32,s=25,v=45,i=1,q=2", pixels)
                + "\x1b_Ga=p,i=1,X=9,Y=19,q=2\x1b\\";
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(command));

        using var snapshot = terminal.CreateSnapshot();
        var placement = TestSeq.Single(snapshot.KgpPlacements);
        Assert.IsTrue(placement.UsesNativeSize);
        Assert.AreEqual(4u, placement.DisplayColumns);
        Assert.AreEqual(4u, placement.DisplayRows);
        Assert.AreEqual(25u, placement.SourceWidth);
        Assert.AreEqual(45u, placement.SourceHeight);
        Assert.AreEqual(4, snapshot.CursorX);
        Assert.AreEqual(3, snapshot.CursorY);
    }

    [TestMethod]
    public void ClipRows_NativeSize_ClipsInPixelSpace()
    {
        var image = new KgpImageData(1, 0, KgpTestHelper.CreatePixelData(10, 30),
            10, 30, KgpFormat.Rgba32);
        var placement = new KgpPlacement(1, 1, 0, 0, 1, 1, cellOffsetY: 5)
            .WithNativeSize(image, 10, 20);
        var topClipped = placement.ClipRows(image, 1, 1, 0, 20);
        var bottomClipped = placement.ClipRows(image, 0, 1, 0, 20);

        Assert.IsNotNull(topClipped);
        Assert.IsTrue(topClipped.UsesNativeSize);
        Assert.AreEqual(15u, topClipped.SourceY);
        Assert.AreEqual(15u, topClipped.SourceHeight);
        Assert.AreEqual(0u, topClipped.CellOffsetY);
        Assert.IsNotNull(bottomClipped);
        Assert.IsTrue(bottomClipped.UsesNativeSize);
        Assert.AreEqual(0u, bottomClipped.SourceY);
        Assert.AreEqual(15u, bottomClipped.SourceHeight);
        Assert.AreEqual(5u, bottomClipped.CellOffsetY);
    }

    [TestMethod]
    public void Snapshot_RelativeNativePlacement_PreservesSizingAndDeletesWithParent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[3;4H" + KgpTestHelper.BuildCommand(
                "a=T,f=32,s=3,v=3,i=1,p=1,C=1,q=2",
                KgpTestHelper.CreatePixelData(3, 3)) +
            "\x1b_Ga=p,i=1,p=2,P=1,Q=1,H=2,V=1,X=9,Y=19,C=1,q=2\x1b\\"));
        using (var snapshot = terminal.CreateSnapshot())
        {
            Assert.AreEqual(2, snapshot.KgpPlacements.Count);
            var child = snapshot.KgpPlacements.Single(p => p.PlacementId == 2);
            Assert.IsTrue(child.UsesNativeSize);
            Assert.IsNull(child.RenderGeometry);
            Assert.AreEqual(5, child.Column);
            Assert.AreEqual(3, child.Row);
            Assert.AreEqual(2u, child.DisplayColumns);
            Assert.AreEqual(2u, child.DisplayRows);
            Assert.AreEqual(3u, child.SourceWidth);
            Assert.AreEqual(3u, child.SourceHeight);
        }

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b_Ga=d,d=a,q=2\x1b\\"));
        using var cleared = terminal.CreateSnapshot();
        Assert.AreEqual(0, cleared.KgpPlacements.Count);
    }

    [TestMethod]
    public void Scroll_NativeSpriteCrossingCellBoundary_PreservesRemainingPixels()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[2;1H" + KgpTestHelper.BuildCommand(
                "a=T,f=32,s=3,v=3,i=1,Y=19,C=1,q=2",
                KgpTestHelper.CreatePixelData(3, 3)) +
            "\x1b[2S"));
        using var snapshot = terminal.CreateSnapshot();
        var placement = TestSeq.Single(snapshot.KgpPlacements);
        Assert.IsTrue(placement.UsesNativeSize);
        Assert.AreEqual(0, placement.Row);
        Assert.AreEqual(0u, placement.CellOffsetY);
        Assert.AreEqual(1u, placement.SourceY);
        Assert.AreEqual(2u, placement.SourceHeight);
    }

    private static Hex1bTerminal CreateTerminal(Hex1bAppWorkloadAdapter workload)
        => Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(new TerminalCapabilities
            {
                SupportsKgp = true,
                CellPixelWidth = 10,
                CellPixelHeight = 20
            })
            .WithDimensions(40, 12)
            .Build();
}
