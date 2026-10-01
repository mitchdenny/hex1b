using System.Xml.Linq;
using Hex1b.Automation;
using Hex1b.Layout;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class LineRenditionRenderingTests
{
    [TestMethod]
    [DataRow('3', "2", 0)]
    [DataRow('4', "2", -20)]
    [DataRow('6', "1", 0)]
    public void Svg_EnlargedRow_ClipsTextAndDecorations(char mode, string scaleY, int offset)
    {
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(10, 3).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            $"\x1b#{mode}\x1b[4:2;9;53;31mA\u754c\x1b[2G"));
        using var snapshot = terminal.CreateSnapshot();
        var svg = XDocument.Parse(snapshot.ToSvg(new TerminalSvgOptions { CellWidth = 10, CellHeight = 20 }));
        XNamespace ns = "http://www.w3.org/2000/svg";
        var transform = svg.Descendants(ns + "g").First(e => e.Attribute("transform") is not null);
        Assert.AreEqual($"translate(0 {offset}) scale(2 {scaleY}) translate(0 0)", (string?)transform.Attribute("transform"));
        Assert.AreEqual("url(#rendition-row-0)", (string?)transform.Parent!.Attribute("clip-path"));
        var clip = svg.Descendants(ns + "clipPath").Single(e => (string?)e.Attribute("id") == "rendition-row-0");
        Assert.AreEqual("20", (string?)clip.Element(ns + "rect")!.Attribute("height"));
        var cursor = svg.Descendants(ns + "rect").Single(e => (string?)e.Attribute("class") == "cursor");
        Assert.AreEqual("20", (string?)cursor.Attribute("x"));
        Assert.AreEqual("20", (string?)cursor.Attribute("width"));
        Assert.AreEqual(24, svg.Descendants(ns + "rect").Count(e => (string?)e.Attribute("class") == "cell-bg"));
        Assert.IsTrue(transform.Descendants(ns + "line").Any());
        var html = snapshot.ToHtml(new TerminalSvgOptions { CellWidth = 10, CellHeight = 20 });
        Assert.Contains("const rowScales = [2,1,1];", html);
        Assert.Contains("const rowWidths = [5,10,10];", html);
        Assert.Contains("x < rowWidths[y]", html);
    }

    [TestMethod]
    public void Svg_OneColumnSnapshot_ClipsToPhysicalViewport()
    {
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(1, 2).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b#6A"));
        using var snapshot = terminal.CreateSnapshot();
        var options = new TerminalSvgOptions { CellWidth = 10, CellHeight = 20 };
        var svg = XDocument.Parse(snapshot.ToSvg(options));
        Assert.AreEqual("10", (string?)svg.Root!.Attribute("width"));
        Assert.Contains("const SVG_WIDTH = 10;", snapshot.ToHtml(options));
        var regionSvg = XDocument.Parse(snapshot.GetRegion(new Rect(0, 0, 1, 1)).ToSvg(options));
        Assert.AreEqual("10", (string?)regionSvg.Root!.Attribute("width"));
    }

    [TestMethod]
    public void Svg_EnlargedSubregion_HasEnoughDisplayWidth()
    {
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(10, 3).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b#6ABCDE"));
        using var snapshot = terminal.CreateSnapshot();
        var region = snapshot.GetRegion(new Rect(1, 0, 3, 1));
        var options = new TerminalSvgOptions { CellWidth = 10, CellHeight = 20 };
        var svg = XDocument.Parse(region.ToSvg(options));
        Assert.AreEqual("60", (string?)svg.Root!.Attribute("width"));
        Assert.Contains("const SVG_WIDTH = 60;", region.ToHtml(options));
    }
}
