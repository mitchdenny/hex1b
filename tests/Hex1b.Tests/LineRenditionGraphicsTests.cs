using Hex1b.Tokens;
using Hex1b.Reflow;
using Hex1b.Automation;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Hex1b.Tests;

[TestClass]
public class LineRenditionGraphicsTests
{
    private const string Sixel = "\x1bP7;1q\"1;1;1;6#1;2;100;0;0#1~\x1b\\";
    private const string Kgp = "\x1b_Ga=T,f=32,s=1,v=1,i=1,p=1,C=1,q=2;/wAA/w==\x1b\\";

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('6')]
    public void Graphics_RenditionChange_DoesNotResizeOrDiscardPhysicalPlacements(char mode)
    {
        using var terminal = Create();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;7H" + Sixel + "\x1b[1;7H" + Kgp));
        using var before = terminal.CreateSnapshot();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b[H\x1b#{mode}"));
        using var after = terminal.CreateSnapshot();
        Assert.AreEqual(1, after.SixelPlacements.Count);
        Assert.AreEqual(before.SixelPlacements[0].Column, after.SixelPlacements[0].Column);
        Assert.AreEqual(before.SixelPlacements[0].PaintedColumnCount, after.SixelPlacements[0].PaintedColumnCount);
        Assert.AreEqual(before.KgpPlacements[0].Column, after.KgpPlacements[0].Column);
        Assert.AreEqual(before.KgpPlacements[0].DisplayColumns, after.KgpPlacements[0].DisplayColumns);
        XNamespace ns = "http://www.w3.org/2000/svg";
        var previousImages = XDocument.Parse(before.ToSvg()).Descendants(ns + "image").ToArray();
        var images = XDocument.Parse(after.ToSvg()).Descendants(ns + "image").ToArray();
        Assert.AreEqual(2, images.Length);
        for (var i = 0; i < images.Length; i++)
        {
            foreach (var attribute in new[] { "x", "y", "width", "height" })
                Assert.AreEqual((string?)previousImages[i].Attribute(attribute), (string?)images[i].Attribute(attribute));
            Assert.IsTrue(images[i].Ancestors().All(element => element.Attribute("transform") is null));
        }
        var json = Regex.Match(after.ToHtml(), @"const cellData = (\[.*?\]);", RegexOptions.Singleline);
        Assert.IsTrue(json.Success);
        using var data = JsonDocument.Parse(json.Groups[1].Value);
        Assert.IsTrue(data.RootElement[0][3].GetProperty("sixel").GetProperty("origin").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, data.RootElement[0][6].GetProperty("sixel").ValueKind);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[4GX"));
        Assert.AreEqual(0, terminal.SixelPlacementCount, "Printing at logical column 3 damages physical column 6.");
        using var written = terminal.CreateSnapshot();
        Assert.AreEqual(1, written.KgpPlacements.Count, "KGP placements are not damaged by text writes.");
    }

    [TestMethod]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('6')]
    public void Graphics_CursorAnchoredOnEnlargedRow_UsesPhysicalOriginWithoutScalingImage(char mode)
    {
        using var terminal = Create();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b#{mode}\x1b[4G" + Kgp + Sixel));
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(6, snapshot.KgpPlacements[0].Column);
        Assert.AreEqual(6, snapshot.SixelPlacements[0].Column);
        Assert.AreEqual(1, snapshot.SixelPlacements[0].PaintedColumnCount);
        for (var col = 6; col < 12; col++)
            Assert.IsTrue(string.IsNullOrWhiteSpace(snapshot.GetCell(col, 0).Character));
        Assert.AreEqual(6, terminal.CursorX, "The next normal row receives a physical-column cursor.");
    }

    private static Hex1bTerminal Create() => Hex1bTerminal.CreateBuilder()
        .WithWorkload(new Hex1bAppWorkloadAdapter())
        .WithHeadless(new TerminalCapabilities { SupportsSixel = true, SupportsKgp = true })
        .WithDimensions(12, 6).Build();

    [TestMethod]
    public void Graphics_EraseAndDeleteOnEnlargedRow_UsePhysicalCursor()
    {
        using var terminal = Create();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b#6\x1b[4G" + Kgp + Sixel));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;4H\x1b[K"));
        Assert.AreEqual(0, terminal.SixelPlacementCount);
        using (var erased = terminal.CreateSnapshot())
            Assert.AreEqual(LineRendition.DoubleWidth, erased.GetLineRendition(0));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b_Ga=d,d=c,q=2\x1b\\"));
        using var snapshot = terminal.CreateSnapshot();
        Assert.IsEmpty(snapshot.KgpPlacements);
    }

    [TestMethod]
    public void Graphics_ResizeEnlargedRow_PreservesPhysicalAnchorBeyondLogicalWidth()
    {
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless(
                new TerminalCapabilities { SupportsSixel = true, SupportsKgp = true })
            .WithDimensions(12, 6).WithReflow(GhosttyReflowStrategy.Instance).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b#6\x1b[4G" + Kgp + Sixel));
        terminal.Resize(10, 6);
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(6, TestSeq.Single(snapshot.KgpPlacements).Column);
        Assert.AreEqual(6, TestSeq.Single(snapshot.SixelPlacements).Column);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public void Sixel_PartiallyOverlappingEnlargedWideGlyph_ClearsWholeGlyph(int column)
    {
        using var terminal = Create();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b#6界\x1b[{column}G" + Sixel));
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(" ", snapshot.GetCell(0, 0).Character);
        Assert.AreEqual(" ", snapshot.GetCell(1, 0).Character);
        Assert.AreEqual(1, snapshot.SixelPlacements.Count);
    }
}
