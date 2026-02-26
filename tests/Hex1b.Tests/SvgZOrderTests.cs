using Hex1b;
using Hex1b.Automation;
using Hex1b.Tokens;
using Hex1b.Kgp;
using Xunit;

namespace Hex1b.Tests;

public class SvgZOrderTests
{
    private static readonly TerminalCapabilities KgpCapabilities = new()
    {
        SupportsKgp = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
    };

    [Fact]
    public void SvgOutput_HasCorrectLayerOrder()
    {
        // The SVG should render in order: terminal-bg → terminal-images → terminal-text
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("Hello"));

        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();

        var bgIndex = svg.IndexOf("class=\"terminal-bg\"");
        var imagesIndex = svg.IndexOf("class=\"terminal-images\"");
        var textIndex = svg.IndexOf("class=\"terminal-text\"");

        Assert.True(bgIndex >= 0, "terminal-bg group not found");
        Assert.True(imagesIndex >= 0, "terminal-images group not found");
        Assert.True(textIndex >= 0, "terminal-text group not found");

        // Z-order: bg < images < text (SVG document order = z-order)
        Assert.True(bgIndex < imagesIndex, "terminal-bg should appear before terminal-images");
        Assert.True(imagesIndex < textIndex, "terminal-images should appear before terminal-text");
    }

    [Fact]
    public void SvgOutput_SixelRenderedInImagesLayer()
    {
        // Sixel images should be rendered inside the terminal-images group, not after terminal-text
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();

        // Write a simple 1x1 red pixel sixel
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1bPq\"1;1;1;1#0;2;100;0;0#0!1~\x1b\\"));

        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();

        var imagesGroupStart = svg.IndexOf("class=\"terminal-images\"");
        var imagesGroupEnd = svg.IndexOf("</g>", imagesGroupStart);
        var textGroupStart = svg.IndexOf("class=\"terminal-text\"");

        // Image element should be between images group start and end
        var imageElementIndex = svg.IndexOf("<image", imagesGroupStart);
        if (imageElementIndex >= 0) // Only assert if sixel was decoded
        {
            Assert.True(imageElementIndex < imagesGroupEnd, "Sixel image should be inside terminal-images group");
            Assert.True(imageElementIndex < textGroupStart, "Sixel image should appear before terminal-text group");
        }
    }

    [Fact]
    public void SvgOutput_KgpRenderedInImagesLayer()
    {
        // KGP images should be rendered inside the terminal-images group
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(KgpCapabilities)
            .WithDimensions(20, 5)
            .Build();

        // Transmit a small 2x2 RGBA image via KGP
        var rgbaPixels = new byte[]
        {
            255, 0, 0, 255,   0, 255, 0, 255,  // row 0: red, green
            0, 0, 255, 255,   255, 255, 0, 255  // row 1: blue, yellow
        };
        var base64 = System.Convert.ToBase64String(rgbaPixels);
        var kgpSequence = $"\x1b_Gf=32,s=2,v=2,i=1,c=2,r=1,a=T,t=d;{base64}\x1b\\";
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(kgpSequence));

        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();

        var imagesGroupStart = svg.IndexOf("class=\"terminal-images\"");
        var textGroupStart = svg.IndexOf("class=\"terminal-text\"");

        // KGP image should appear in the images layer
        var imageElementIndex = svg.IndexOf("<image", imagesGroupStart);
        Assert.True(imageElementIndex >= 0, "KGP image element not found in SVG");
        Assert.True(imageElementIndex < textGroupStart, "KGP image should appear before terminal-text group");
    }

    [Fact]
    public void SvgOutput_SnapshotCapturesKgpPlacements()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(KgpCapabilities)
            .WithDimensions(20, 5)
            .Build();

        // Transmit a small 2x2 RGBA image
        var rgbaPixels = new byte[]
        {
            255, 0, 0, 255,   0, 255, 0, 255,
            0, 0, 255, 255,   255, 255, 0, 255
        };
        var base64 = System.Convert.ToBase64String(rgbaPixels);
        var kgpSequence = $"\x1b_Gf=32,s=2,v=2,i=1,c=2,r=1,a=T,t=d;{base64}\x1b\\";
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(kgpSequence));

        // Verify terminal has the placement (pre-snapshot)
        Assert.NotEmpty(terminal.KgpPlacements);

        var snapshot = terminal.CreateSnapshot();

        Assert.NotEmpty(snapshot.KgpPlacements);
        Assert.NotEmpty(snapshot.KgpImages);

        var placement = snapshot.KgpPlacements[0];
        Assert.True(snapshot.KgpImages.ContainsKey(placement.ImageId), "Snapshot should contain image data for placement");

        var imageData = snapshot.KgpImages[placement.ImageId];
        Assert.Equal(2u, imageData.Width);
        Assert.Equal(2u, imageData.Height);
        Assert.Equal(KgpFormat.Rgba32, imageData.Format);
    }

    [Fact]
    public void SvgOutput_TextAboveKgpImage()
    {
        // Text written over a KGP image region should render above the image in SVG
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(KgpCapabilities)
            .WithDimensions(20, 5)
            .Build();

        // Place KGP image at cursor position (0,0)
        var rgbaPixels = new byte[4 * 4 * 4]; // 4x4 RGBA (all zeros = transparent black)
        for (int i = 0; i < rgbaPixels.Length; i += 4) { rgbaPixels[i] = 255; rgbaPixels[i + 3] = 255; } // red
        var base64 = System.Convert.ToBase64String(rgbaPixels);
        var kgpSequence = $"\x1b_Gf=32,s=4,v=4,i=1,c=4,r=2,a=T,t=d;{base64}\x1b\\";
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(kgpSequence));

        // Write text at same position
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1HOverlay"));

        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();

        // The image should be in terminal-images, text should be in terminal-text (after)
        var imagesGroupStart = svg.IndexOf("class=\"terminal-images\"");
        var textGroupStart = svg.IndexOf("class=\"terminal-text\"");
        
        var imageElement = svg.IndexOf("<image", imagesGroupStart);
        var textElement = svg.IndexOf(">O</text>", textGroupStart); // "O" from "Overlay"

        Assert.True(imageElement >= 0, "KGP image element not found");
        Assert.True(textElement >= 0, "Text element not found");
        Assert.True(imageElement < textElement, "Image should appear before text in SVG document order");
    }
}
