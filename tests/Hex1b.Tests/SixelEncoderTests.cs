using Hex1b;
using Hex1b.Automation;
using Hex1b.Surfaces;
using Hex1b.Theming;

namespace Hex1b.Tests;

/// <summary>
/// Tests for <see cref="SixelEncoder"/> and round-trip verification with <see cref="SixelDecoder"/>.
/// </summary>
public class SixelEncoderTests
{
    #region T1: Encoder/Decoder Round-Trip Tests

    [Fact]
    public void T1_1_SolidColor_RoundTrip()
    {
        // Single solid red color
        var buffer = CreateSolidBuffer(10, 12, Rgba32.FromRgb(255, 0, 0));
        
        var encoded = SixelEncoder.Encode(buffer);
        var decoded = SixelDecoder.Decode(encoded);
        
        Assert.NotNull(decoded);
        Assert.Equal(10, decoded.Width);
        Assert.Equal(12, decoded.Height);
        
        // Check center pixel is red (allowing for quantization)
        AssertPixelSimilar(decoded, 5, 6, 255, 0, 0);
    }

    [Fact]
    public void T1_2_TwoColors_RoundTrip()
    {
        // Left half red, right half blue
        var buffer = new SixelPixelBuffer(20, 12);
        for (var y = 0; y < 12; y++)
        {
            for (var x = 0; x < 10; x++)
                buffer[x, y] = Rgba32.FromRgb(255, 0, 0);
            for (var x = 10; x < 20; x++)
                buffer[x, y] = Rgba32.FromRgb(0, 0, 255);
        }
        
        var encoded = SixelEncoder.Encode(buffer);
        var decoded = SixelDecoder.Decode(encoded);
        
        Assert.NotNull(decoded);
        AssertPixelSimilar(decoded, 2, 6, 255, 0, 0);   // Left half - red
        AssertPixelSimilar(decoded, 15, 6, 0, 0, 255);  // Right half - blue
    }

    [Fact]
    public void T1_3_FullPaletteUsage()
    {
        // Create image with 256 distinct colors
        var buffer = new SixelPixelBuffer(16, 16);
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                var colorIndex = y * 16 + x;
                var r = (byte)((colorIndex & 0x07) << 5);
                var g = (byte)(((colorIndex >> 3) & 0x07) << 5);
                var b = (byte)(((colorIndex >> 6) & 0x03) << 6);
                buffer[x, y] = Rgba32.FromRgb(r, g, b);
            }
        }
        
        var encoded = SixelEncoder.Encode(buffer);
        var decoded = SixelDecoder.Decode(encoded);
        
        Assert.NotNull(decoded);
        Assert.Equal(16, decoded.Width);
        // Height may be padded to band boundary (multiple of 6)
        Assert.True(decoded.Height >= 16);
    }

    [Fact]
    public void T1_4_Gradient_Quantization()
    {
        // Horizontal red gradient
        var buffer = new SixelPixelBuffer(100, 12);
        for (var y = 0; y < 12; y++)
        {
            for (var x = 0; x < 100; x++)
            {
                var r = (byte)(x * 255 / 99);
                buffer[x, y] = Rgba32.FromRgb(r, 0, 0);
            }
        }
        
        var encoded = SixelEncoder.Encode(buffer);
        var decoded = SixelDecoder.Decode(encoded);
        
        Assert.NotNull(decoded);
        
        // Left should be dark, right should be bright
        var leftPixel = GetPixel(decoded, 5, 6);
        var rightPixel = GetPixel(decoded, 94, 6);
        Assert.True(rightPixel.r > leftPixel.r, "Gradient should go from dark to bright");
    }

    [Fact]
    public void T1_5_ScatteredTransparency()
    {
        // Checkerboard of opaque and transparent pixels
        var buffer = new SixelPixelBuffer(20, 12);
        for (var y = 0; y < 12; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                buffer[x, y] = ((x + y) % 2 == 0)
                    ? Rgba32.FromRgb(255, 0, 0)
                    : Rgba32.Transparent;
            }
        }
        
        var encoded = SixelEncoder.Encode(buffer);
        var decoded = SixelDecoder.Decode(encoded);
        
        Assert.NotNull(decoded);
        
        // Check opaque pixel
        var opaquePixel = GetPixel(decoded, 0, 0);
        Assert.Equal(255, opaquePixel.a);
        
        // Check transparent pixel
        var transparentPixel = GetPixel(decoded, 1, 0);
        Assert.Equal(0, transparentPixel.a);
    }

    [Fact]
    public void T1_6_ContiguousTransparency()
    {
        // Top half opaque, bottom half transparent
        var buffer = new SixelPixelBuffer(20, 12);
        for (var y = 0; y < 6; y++)
        {
            for (var x = 0; x < 20; x++)
                buffer[x, y] = Rgba32.FromRgb(0, 255, 0);
        }
        // Bottom half stays transparent (default)
        
        var encoded = SixelEncoder.Encode(buffer);
        var decoded = SixelDecoder.Decode(encoded);
        
        Assert.NotNull(decoded);
        
        // Top should be green
        AssertPixelSimilar(decoded, 10, 2, 0, 255, 0);
        
        // Bottom should be transparent
        var bottomPixel = GetPixel(decoded, 10, 10);
        Assert.Equal(0, bottomPixel.a);
    }

    [Fact]
    public void T1_7_SinglePixel()
    {
        var buffer = CreateSolidBuffer(1, 1, Rgba32.FromRgb(128, 128, 128));
        
        var encoded = SixelEncoder.Encode(buffer);
        var decoded = SixelDecoder.Decode(encoded);
        
        Assert.NotNull(decoded);
        Assert.Equal(1, decoded.Width);
        // Height will be padded to band boundary (6)
        Assert.True(decoded.Height >= 1);
    }

    [Fact]
    public void T1_8_SingleRow()
    {
        var buffer = new SixelPixelBuffer(50, 1);
        for (var x = 0; x < 50; x++)
            buffer[x, 0] = Rgba32.FromRgb(255, 255, 0);
        
        var encoded = SixelEncoder.Encode(buffer);
        var decoded = SixelDecoder.Decode(encoded);
        
        Assert.NotNull(decoded);
        Assert.Equal(50, decoded.Width);
    }

    [Fact]
    public void T1_9_SingleColumn()
    {
        var buffer = new SixelPixelBuffer(1, 50);
        for (var y = 0; y < 50; y++)
            buffer[0, y] = Rgba32.FromRgb(0, 255, 255);
        
        var encoded = SixelEncoder.Encode(buffer);
        var decoded = SixelDecoder.Decode(encoded);
        
        Assert.NotNull(decoded);
        Assert.True(decoded.Height >= 50);
    }

    [Fact]
    public void T1_10_LargeImage()
    {
        // 100x100 should work (though we won't test 1000x1000 for speed)
        var buffer = CreateSolidBuffer(100, 100, Rgba32.FromRgb(100, 150, 200));
        
        var encoded = SixelEncoder.Encode(buffer);
        var decoded = SixelDecoder.Decode(encoded);
        
        Assert.NotNull(decoded);
        Assert.Equal(100, decoded.Width);
        // Height may be padded to band boundary (multiple of 6)
        Assert.True(decoded.Height >= 100);
    }

    [Fact]
    public void T1_11_NonMultipleOf6Height()
    {
        // Height 10 is not a multiple of 6 (band size)
        var buffer = CreateSolidBuffer(20, 10, Rgba32.FromRgb(200, 100, 50));
        
        var encoded = SixelEncoder.Encode(buffer);
        var decoded = SixelDecoder.Decode(encoded);
        
        Assert.NotNull(decoded);
        Assert.Equal(20, decoded.Width);
        Assert.True(decoded.Height >= 10);
    }

    [Fact]
    public void T1_12_WideImage()
    {
        // Very wide image
        var buffer = CreateSolidBuffer(500, 6, Rgba32.FromRgb(50, 100, 150));
        
        var encoded = SixelEncoder.Encode(buffer);
        var decoded = SixelDecoder.Decode(encoded);
        
        Assert.NotNull(decoded);
        Assert.Equal(500, decoded.Width);
    }

    #endregion

    #region Encoder Format Tests

    [Fact]
    public void Encode_ContainsDcsHeader()
    {
        var buffer = CreateSolidBuffer(10, 6, Rgba32.FromRgb(255, 0, 0));
        var encoded = SixelEncoder.Encode(buffer);
        
        Assert.StartsWith("\x1bP", encoded); // DCS
        Assert.Contains("q", encoded);        // Sixel introducer
    }

    [Fact]
    public void Encode_ContainsStringTerminator()
    {
        var buffer = CreateSolidBuffer(10, 6, Rgba32.FromRgb(255, 0, 0));
        var encoded = SixelEncoder.Encode(buffer);
        
        Assert.EndsWith("\x1b\\", encoded); // ST
    }

    [Fact]
    public void Encode_ContainsColorDefinitions()
    {
        var buffer = CreateSolidBuffer(10, 6, Rgba32.FromRgb(255, 0, 0));
        var encoded = SixelEncoder.Encode(buffer);
        
        // Should contain color definition: #<index>;2;<r>;<g>;<b>
        Assert.Contains("#0;2;", encoded);
    }

    [Fact]
    public void Encode_ContainsRasterAttributes()
    {
        var buffer = CreateSolidBuffer(20, 12, Rgba32.FromRgb(0, 255, 0));
        var encoded = SixelEncoder.Encode(buffer);
        
        // Should contain raster attributes: "1;1;<width>;<height>
        Assert.Contains("\"1;1;20;12", encoded);
    }

    [Fact]
    public void Encode_UsesRleForLongRuns()
    {
        // Solid color should produce RLE sequences
        var buffer = CreateSolidBuffer(100, 6, Rgba32.FromRgb(255, 255, 255));
        var encoded = SixelEncoder.Encode(buffer);
        
        // Should contain RLE: !<count><char>
        Assert.Contains("!", encoded);
    }

    [Fact]
    public void Encode_AllTransparent_ReturnsMinimalSixel()
    {
        var buffer = new SixelPixelBuffer(10, 10);
        // All pixels are transparent by default
        
        var encoded = SixelEncoder.Encode(buffer);
        
        Assert.Contains("\x1bP", encoded);
        Assert.Contains("\x1b\\", encoded);
    }

    #endregion

    #region SixelPixelBuffer Tests

    [Fact]
    public void SixelPixelBuffer_Crop_ReturnsCorrectRegion()
    {
        var buffer = new SixelPixelBuffer(20, 20);
        
        // Mark a specific region
        for (var y = 5; y < 15; y++)
            for (var x = 5; x < 15; x++)
                buffer[x, y] = Rgba32.FromRgb(255, 0, 0);
        
        var cropped = buffer.Crop(5, 5, 10, 10);
        
        Assert.Equal(10, cropped.Width);
        Assert.Equal(10, cropped.Height);
        Assert.Equal(Rgba32.FromRgb(255, 0, 0), cropped[0, 0]);
        Assert.Equal(Rgba32.FromRgb(255, 0, 0), cropped[9, 9]);
    }

    [Fact]
    public void SixelPixelBuffer_Crop_ClampsToValidBounds()
    {
        var buffer = new SixelPixelBuffer(10, 10);
        for (var y = 0; y < 10; y++)
            for (var x = 0; x < 10; x++)
                buffer[x, y] = Rgba32.FromRgb(0, 255, 0);
        
        // Request region that extends past bounds
        var cropped = buffer.Crop(5, 5, 20, 20);
        
        Assert.Equal(5, cropped.Width);
        Assert.Equal(5, cropped.Height);
    }

    [Fact]
    public void SixelPixelBuffer_GetPixelOrTransparent_ReturnsTransparentForOutOfBounds()
    {
        var buffer = new SixelPixelBuffer(10, 10);
        buffer[5, 5] = Rgba32.FromRgb(255, 0, 0);
        
        Assert.Equal(Rgba32.Transparent, buffer.GetPixelOrTransparent(-1, 5));
        Assert.Equal(Rgba32.Transparent, buffer.GetPixelOrTransparent(5, -1));
        Assert.Equal(Rgba32.Transparent, buffer.GetPixelOrTransparent(10, 5));
        Assert.Equal(Rgba32.Transparent, buffer.GetPixelOrTransparent(5, 10));
        Assert.Equal(Rgba32.FromRgb(255, 0, 0), buffer.GetPixelOrTransparent(5, 5));
    }

    #endregion

    #region T2: PixelRect Tests

    [Fact]
    public void T2_1_PixelRect_Properties()
    {
        var rect = new PixelRect(10, 20, 30, 40);
        
        Assert.Equal(10, rect.X);
        Assert.Equal(20, rect.Y);
        Assert.Equal(30, rect.Width);
        Assert.Equal(40, rect.Height);
        Assert.Equal(40, rect.Right);
        Assert.Equal(60, rect.Bottom);
        Assert.Equal(1200, rect.Area);
        Assert.False(rect.IsEmpty);
    }

    [Fact]
    public void T2_2_PixelRect_IsEmpty()
    {
        Assert.True(new PixelRect(0, 0, 0, 10).IsEmpty);
        Assert.True(new PixelRect(0, 0, 10, 0).IsEmpty);
        Assert.True(new PixelRect(0, 0, -5, 10).IsEmpty);
        Assert.False(new PixelRect(0, 0, 1, 1).IsEmpty);
    }

    [Fact]
    public void T2_3_PixelRect_Intersect_Overlapping()
    {
        var a = new PixelRect(0, 0, 20, 20);
        var b = new PixelRect(10, 10, 20, 20);
        
        var intersection = a.Intersect(b);
        
        Assert.Equal(10, intersection.X);
        Assert.Equal(10, intersection.Y);
        Assert.Equal(10, intersection.Width);
        Assert.Equal(10, intersection.Height);
    }

    [Fact]
    public void T2_4_PixelRect_Intersect_NoOverlap()
    {
        var a = new PixelRect(0, 0, 10, 10);
        var b = new PixelRect(20, 20, 10, 10);
        
        var intersection = a.Intersect(b);
        
        Assert.True(intersection.IsEmpty);
    }

    [Fact]
    public void T2_5_PixelRect_Intersect_Contained()
    {
        var outer = new PixelRect(0, 0, 100, 100);
        var inner = new PixelRect(25, 25, 50, 50);
        
        var intersection = outer.Intersect(inner);
        
        Assert.Equal(inner, intersection);
    }

    [Fact]
    public void T2_6_PixelRect_Contains_Point()
    {
        var rect = new PixelRect(10, 10, 20, 20);
        
        Assert.True(rect.Contains(10, 10));   // Top-left corner
        Assert.True(rect.Contains(29, 29));   // Bottom-right (exclusive edge -1)
        Assert.True(rect.Contains(20, 20));   // Center
        Assert.False(rect.Contains(9, 10));   // Left of
        Assert.False(rect.Contains(30, 10));  // Right edge (exclusive)
        Assert.False(rect.Contains(10, 30));  // Bottom edge (exclusive)
    }

    [Fact]
    public void T2_7_PixelRect_Contains_Rect()
    {
        var outer = new PixelRect(0, 0, 100, 100);
        var inner = new PixelRect(25, 25, 50, 50);
        var partial = new PixelRect(50, 50, 100, 100);
        
        Assert.True(outer.Contains(inner));
        Assert.False(outer.Contains(partial));
        Assert.False(inner.Contains(outer));
    }

    [Fact]
    public void T2_8_PixelRect_Subtract_NoOverlap()
    {
        var rect = new PixelRect(0, 0, 10, 10);
        var hole = new PixelRect(20, 20, 10, 10);
        
        var fragments = rect.Subtract(hole);
        
        Assert.Single(fragments);
        Assert.Equal(rect, fragments[0]);
    }

    [Fact]
    public void T2_9_PixelRect_Subtract_CenterHole()
    {
        // 10x10 rectangle with 4x4 hole in center
        var rect = new PixelRect(0, 0, 10, 10);
        var hole = new PixelRect(3, 3, 4, 4);
        
        var fragments = rect.Subtract(hole);
        
        // Should produce 4 fragments: top, bottom, left, right
        Assert.Equal(4, fragments.Count);
        
        // Verify total area equals original minus hole
        var totalArea = fragments.Sum(f => f.Area);
        Assert.Equal(rect.Area - hole.Area, totalArea);
    }

    [Fact]
    public void T2_10_PixelRect_Subtract_EdgeHole()
    {
        var rect = new PixelRect(0, 0, 10, 10);
        var hole = new PixelRect(0, 0, 5, 5); // Top-left corner
        
        var fragments = rect.Subtract(hole);
        
        // Should produce 2 fragments: bottom and right
        Assert.Equal(2, fragments.Count);
        var totalArea = fragments.Sum(f => f.Area);
        Assert.Equal(rect.Area - hole.Area, totalArea);
    }

    [Fact]
    public void T2_11_PixelRect_Subtract_FullyCovered()
    {
        var rect = new PixelRect(5, 5, 10, 10);
        var hole = new PixelRect(0, 0, 20, 20); // Covers entire rect
        
        var fragments = rect.Subtract(hole);
        
        Assert.Empty(fragments);
    }

    #endregion

    #region T3: Fragment and Visibility Tests

    [Fact]
    public void T3_1_Fragment_SingleRegion()
    {
        var buffer = new SixelPixelBuffer(20, 20);
        for (var y = 0; y < 20; y++)
            for (var x = 0; x < 20; x++)
                buffer[x, y] = Rgba32.FromRgb((byte)(x * 10), (byte)(y * 10), 0);
        
        var regions = new[] { new PixelRect(5, 5, 10, 10) };
        var fragments = buffer.Fragment(regions);
        
        Assert.Single(fragments);
        Assert.Equal(new PixelRect(5, 5, 10, 10), fragments[0].Region);
        Assert.Equal(10, fragments[0].Buffer.Width);
        Assert.Equal(10, fragments[0].Buffer.Height);
        // First pixel should be from (5,5) in original
        Assert.Equal(Rgba32.FromRgb(50, 50, 0), fragments[0].Buffer[0, 0]);
    }

    [Fact]
    public void T3_2_Fragment_MultipleRegions()
    {
        var buffer = new SixelPixelBuffer(30, 30);
        for (var y = 0; y < 30; y++)
            for (var x = 0; x < 30; x++)
                buffer[x, y] = Rgba32.FromRgb(100, 100, 100);
        
        var regions = new[]
        {
            new PixelRect(0, 0, 10, 10),
            new PixelRect(20, 0, 10, 10),
            new PixelRect(0, 20, 10, 10),
            new PixelRect(20, 20, 10, 10)
        };
        
        var fragments = buffer.Fragment(regions);
        
        Assert.Equal(4, fragments.Count);
        Assert.All(fragments, f =>
        {
            Assert.Equal(10, f.Buffer.Width);
            Assert.Equal(10, f.Buffer.Height);
        });
    }

    [Fact]
    public void T3_3_Fragment_EmptyRegionSkipped()
    {
        var buffer = new SixelPixelBuffer(20, 20);
        
        var regions = new[]
        {
            new PixelRect(0, 0, 10, 10),
            new PixelRect(0, 0, 0, 0), // Empty
            new PixelRect(10, 10, 10, 10)
        };
        
        var fragments = buffer.Fragment(regions);
        
        Assert.Equal(2, fragments.Count);
    }

    [Fact]
    public void T3_4_Fragment_RegionClampedToBounds()
    {
        var buffer = new SixelPixelBuffer(10, 10);
        
        var regions = new[] { new PixelRect(5, 5, 100, 100) }; // Extends way past bounds
        var fragments = buffer.Fragment(regions);
        
        Assert.Single(fragments);
        Assert.Equal(new PixelRect(5, 5, 5, 5), fragments[0].Region);
        Assert.Equal(5, fragments[0].Buffer.Width);
        Assert.Equal(5, fragments[0].Buffer.Height);
    }

    [Fact]
    public void T3_5_ComputeVisibleRegions_NoOcclusions()
    {
        var buffer = new SixelPixelBuffer(100, 60);
        
        var visible = buffer.ComputeVisibleRegions(Array.Empty<PixelRect>());
        
        Assert.Single(visible);
        Assert.Equal(new PixelRect(0, 0, 100, 60), visible[0]);
    }

    [Fact]
    public void T3_6_ComputeVisibleRegions_CenterOcclusion()
    {
        var buffer = new SixelPixelBuffer(100, 60);
        var occlusion = new PixelRect(25, 15, 50, 30); // Center hole
        
        var visible = buffer.ComputeVisibleRegions(new[] { occlusion });
        
        Assert.Equal(4, visible.Count);
        var totalArea = visible.Sum(r => r.Area);
        Assert.Equal(100 * 60 - 50 * 30, totalArea);
    }

    [Fact]
    public void T3_7_ComputeVisibleRegions_FullyOccluded()
    {
        var buffer = new SixelPixelBuffer(50, 50);
        var occlusion = new PixelRect(0, 0, 100, 100); // Covers everything
        
        var visible = buffer.ComputeVisibleRegions(new[] { occlusion });
        
        Assert.Empty(visible);
    }

    [Fact]
    public void T3_8_ComputeVisibleRegions_MultipleOcclusions()
    {
        var buffer = new SixelPixelBuffer(100, 60);
        var occlusions = new[]
        {
            new PixelRect(0, 0, 30, 30),   // Top-left
            new PixelRect(70, 30, 30, 30)  // Bottom-right
        };
        
        var visible = buffer.ComputeVisibleRegions(occlusions);
        
        // Multiple fragments expected
        Assert.True(visible.Count >= 2);
        // No visible region should overlap with occlusions
        foreach (var region in visible)
        {
            foreach (var occ in occlusions)
            {
                Assert.True(region.Intersect(occ).IsEmpty);
            }
        }
    }

    [Fact]
    public void T3_9_FragmentAndEncode_RoundTrip()
    {
        // Create a gradient buffer
        var buffer = new SixelPixelBuffer(40, 24);
        for (var y = 0; y < 24; y++)
            for (var x = 0; x < 40; x++)
                buffer[x, y] = Rgba32.FromRgb((byte)(x * 6), (byte)(y * 10), 128);
        
        // Fragment into quadrants
        var regions = new[]
        {
            new PixelRect(0, 0, 20, 12),
            new PixelRect(20, 0, 20, 12),
            new PixelRect(0, 12, 20, 12),
            new PixelRect(20, 12, 20, 12)
        };
        
        var fragments = buffer.Fragment(regions);
        
        // Encode and decode each fragment
        foreach (var (region, fragmentBuffer) in fragments)
        {
            var encoded = SixelEncoder.Encode(fragmentBuffer);
            var decoded = SixelDecoder.Decode(encoded);
            
            Assert.NotNull(decoded);
            Assert.Equal(fragmentBuffer.Width, decoded.Width);
            Assert.True(decoded.Height >= fragmentBuffer.Height);
        }
    }

    #endregion

    #region T4: CellMetrics Tests

    [Fact]
    public void T4_1_CellMetrics_Default()
    {
        var metrics = CellMetrics.Default;
        
        Assert.Equal(10, metrics.PixelWidth);
        Assert.Equal(20, metrics.PixelHeight);
    }

    [Fact]
    public void T4_2_CellMetrics_CellToPixel()
    {
        var metrics = new CellMetrics(10, 20);
        
        var pixelRect = metrics.CellToPixel(5, 3, 10, 5);
        
        Assert.Equal(50, pixelRect.X);    // 5 * 10
        Assert.Equal(60, pixelRect.Y);    // 3 * 20
        Assert.Equal(100, pixelRect.Width);  // 10 * 10
        Assert.Equal(100, pixelRect.Height); // 5 * 20
    }

    [Fact]
    public void T4_3_CellMetrics_PixelToCellSpan()
    {
        var metrics = new CellMetrics(10, 20);
        
        // Exact multiple
        var (w1, h1) = metrics.PixelToCellSpan(100, 60);
        Assert.Equal(10, w1);
        Assert.Equal(3, h1);
        
        // Rounds up
        var (w2, h2) = metrics.PixelToCellSpan(101, 61);
        Assert.Equal(11, w2);
        Assert.Equal(4, h2);
    }

    [Fact]
    public void T4_4_CellMetrics_PixelToCell()
    {
        var metrics = new CellMetrics(10, 20);
        
        var cellRect = metrics.PixelToCell(new PixelRect(50, 60, 100, 100));
        
        Assert.Equal(5, cellRect.X);   // 50 / 10
        Assert.Equal(3, cellRect.Y);   // 60 / 20
        Assert.Equal(10, cellRect.Width);  // 100 / 10
        Assert.Equal(5, cellRect.Height);  // 100 / 20
    }

    [Fact]
    public void T4_5_CellMetrics_RoundTrip()
    {
        var metrics = new CellMetrics(8, 16);
        
        // Cell -> Pixel -> Cell should give same cell coordinates
        var originalCell = new Hex1b.Layout.Rect(3, 5, 10, 8);
        var pixel = metrics.CellToPixel(originalCell);
        var backToCell = metrics.PixelToCell(pixel);
        
        Assert.Equal(originalCell.X, backToCell.X);
        Assert.Equal(originalCell.Y, backToCell.Y);
        Assert.Equal(originalCell.Width, backToCell.Width);
        Assert.Equal(originalCell.Height, backToCell.Height);
    }

    #endregion

    #region Helper Methods

    private static SixelPixelBuffer CreateSolidBuffer(int width, int height, Rgba32 color)
    {
        var buffer = new SixelPixelBuffer(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                buffer[x, y] = color;
        return buffer;
    }

    private static (byte r, byte g, byte b, byte a) GetPixel(SixelImage image, int x, int y)
    {
        var index = (y * image.Width + x) * 4;
        return (
            image.Pixels[index],
            image.Pixels[index + 1],
            image.Pixels[index + 2],
            image.Pixels[index + 3]);
    }

    private static void AssertPixelSimilar(SixelImage image, int x, int y, byte expectedR, byte expectedG, byte expectedB, int tolerance = 20)
    {
        var (r, g, b, a) = GetPixel(image, x, y);
        
        Assert.True(a > 0, $"Pixel at ({x}, {y}) should not be transparent");
        Assert.True(Math.Abs(r - expectedR) <= tolerance, $"Red at ({x}, {y}): expected ~{expectedR}, got {r}");
        Assert.True(Math.Abs(g - expectedG) <= tolerance, $"Green at ({x}, {y}): expected ~{expectedG}, got {g}");
        Assert.True(Math.Abs(b - expectedB) <= tolerance, $"Blue at ({x}, {y}): expected ~{expectedB}, got {b}");
    }

    #endregion
}

/// <summary>
/// Tests for sixel visibility and fragment generation.
/// </summary>
public class SixelVisibilityTests
{
    private readonly TrackedObjectStore _store = new();

    #region SixelVisibility Tests

    [Fact]
    public void SixelVisibility_InitiallyFullyVisible()
    {
        var sixelRef = CreateTestSixel(100, 60); // 100x60 pixels = 10x3 cells at 10x20 metrics
        var visibility = new SixelVisibility(sixelRef, 5, 5, 0);
        
        Assert.True(visibility.IsFullyVisible);
        Assert.False(visibility.IsFullyOccluded);
        Assert.False(visibility.IsFragmented);
        Assert.Single(visibility.VisibleRegions);
    }

    [Fact]
    public void SixelVisibility_ApplyOcclusion_PartiallyOccludes()
    {
        var sixelRef = CreateTestSixel(100, 60);
        var visibility = new SixelVisibility(sixelRef, 0, 0, 0);
        var metrics = new CellMetrics(10, 20);
        
        // Occlude center 2x1 cells (cells 4-5, row 1)
        var occlusion = new Hex1b.Layout.Rect(4, 1, 2, 1);
        visibility.ApplyOcclusion(occlusion, metrics);
        
        Assert.False(visibility.IsFullyVisible);
        Assert.False(visibility.IsFullyOccluded);
        // Should have 4 fragments: top, bottom, left, right of occlusion
        Assert.Equal(4, visibility.VisibleRegions.Count);
    }

    [Fact]
    public void SixelVisibility_ApplyOcclusion_FullyOccludes()
    {
        var sixelRef = CreateTestSixel(100, 60);
        var visibility = new SixelVisibility(sixelRef, 0, 0, 0);
        var metrics = new CellMetrics(10, 20);
        
        // Occlude entire sixel (10x3 cells)
        var occlusion = new Hex1b.Layout.Rect(0, 0, 10, 3);
        visibility.ApplyOcclusion(occlusion, metrics);
        
        Assert.True(visibility.IsFullyOccluded);
        Assert.Empty(visibility.VisibleRegions);
    }

    [Fact]
    public void SixelVisibility_ApplyOcclusion_NoOverlap_NoChange()
    {
        var sixelRef = CreateTestSixel(100, 60);
        var visibility = new SixelVisibility(sixelRef, 0, 0, 0);
        var metrics = new CellMetrics(10, 20);
        
        // Occlude area outside sixel
        var occlusion = new Hex1b.Layout.Rect(20, 20, 5, 5);
        visibility.ApplyOcclusion(occlusion, metrics);
        
        Assert.True(visibility.IsFullyVisible);
        Assert.Single(visibility.VisibleRegions);
    }

    [Fact]
    public void SixelVisibility_GenerateFragments_FullyVisible_SingleFragment()
    {
        var sixelRef = CreateTestSixel(100, 60);
        var visibility = new SixelVisibility(sixelRef, 5, 3, 0);
        var metrics = new CellMetrics(10, 20);
        
        var fragments = visibility.GenerateFragments(metrics);
        
        Assert.Single(fragments);
        Assert.Equal((5, 3), fragments[0].CellPosition);
        Assert.True(fragments[0].IsComplete);
    }

    [Fact]
    public void SixelVisibility_GenerateFragments_Occluded_MultipleFragments()
    {
        var sixelRef = CreateTestSixel(100, 60);
        var visibility = new SixelVisibility(sixelRef, 0, 0, 0);
        var metrics = new CellMetrics(10, 20);
        
        // Occlude center
        var occlusion = new Hex1b.Layout.Rect(4, 1, 2, 1);
        visibility.ApplyOcclusion(occlusion, metrics);
        
        var fragments = visibility.GenerateFragments(metrics);
        
        Assert.True(fragments.Count >= 2);
        Assert.All(fragments, f => Assert.False(f.IsComplete));
    }

    #endregion

    #region CompositeSurface.GetSixelFragments Tests

    [Fact]
    public void GetSixelFragments_NoSixels_ReturnsEmpty()
    {
        var composite = new CompositeSurface(80, 24);
        var layer = new Surface(40, 12);
        layer.WriteText(0, 0, "Hello");
        composite.AddLayer(layer, 0, 0);
        
        var fragments = composite.GetSixelFragments();
        
        Assert.Empty(fragments);
    }

    [Fact]
    public void GetSixelFragments_UnoccludedSixel_SingleFragment()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(80, 24, metrics);
        var layer = new Surface(40, 12, metrics);
        
        var sixelRef = CreateTestSixel(100, 60);
        var sixelCell = new SurfaceCell(" ", null, null, Sixel: sixelRef);
        layer[5, 3] = sixelCell;
        
        composite.AddLayer(layer, 0, 0);
        
        var fragments = composite.GetSixelFragments();
        
        Assert.Single(fragments);
        Assert.Equal((5, 3), fragments[0].CellPosition);
    }

    [Fact]
    public void GetSixelFragments_PartiallyOccluded_MultipleFragments()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(80, 24, metrics);
        
        // Background layer with sixel
        var background = new Surface(40, 12, metrics);
        var sixelRef = CreateTestSixel(100, 60); // 10x3 cells
        var sixelCell = new SurfaceCell(" ", null, null, Sixel: sixelRef);
        background[0, 0] = sixelCell;
        composite.AddLayer(background, 0, 0);
        
        // Foreground layer with opaque dialog covering center
        var dialog = new Surface(4, 2, metrics);
        dialog.Fill(new Hex1b.Layout.Rect(0, 0, 4, 2), 
            new SurfaceCell(" ", null, Hex1bColor.Blue));
        composite.AddLayer(dialog, 3, 0); // Covers cells (3,0) to (6,1)
        
        var fragments = composite.GetSixelFragments();
        
        // Should have multiple fragments due to occlusion
        Assert.True(fragments.Count >= 2);
    }

    [Fact]
    public void GetSixelFragments_FullyOccluded_ReturnsEmpty()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(80, 24, metrics);
        
        // Background layer with small sixel
        var background = new Surface(40, 12, metrics);
        var sixelRef = CreateTestSixel(20, 20); // 2x1 cells
        var sixelCell = new SurfaceCell(" ", null, null, Sixel: sixelRef);
        background[5, 5] = sixelCell;
        composite.AddLayer(background, 0, 0);
        
        // Foreground layer fully covering the sixel
        var overlay = new Surface(5, 5, metrics);
        overlay.Fill(new Hex1b.Layout.Rect(0, 0, 5, 5), 
            new SurfaceCell(" ", null, Hex1bColor.Red));
        composite.AddLayer(overlay, 4, 4);
        
        var fragments = composite.GetSixelFragments();
        
        Assert.Empty(fragments);
    }

    [Fact]
    public void GetSixelFragments_MultipleSixels_ReturnsAll()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(80, 24, metrics);
        var layer = new Surface(40, 12, metrics);
        
        var sixelRef1 = CreateTestSixel(50, 40);
        var sixelRef2 = CreateTestSixel(60, 40);
        
        layer[0, 0] = new SurfaceCell(" ", null, null, Sixel: sixelRef1);
        layer[20, 5] = new SurfaceCell(" ", null, null, Sixel: sixelRef2);
        
        composite.AddLayer(layer, 0, 0);
        
        var fragments = composite.GetSixelFragments();
        
        Assert.Equal(2, fragments.Count);
    }

    #endregion

    #region SixelFragment Tests

    [Fact]
    public void SixelFragment_Complete_ReturnsOriginalPayload()
    {
        var sixelRef = CreateTestSixel(100, 60);
        var fragment = SixelFragment.Complete(sixelRef.Data, 0, 0);
        
        Assert.True(fragment.IsComplete);
        Assert.Equal(sixelRef.Data.Payload, fragment.GetPayload());
    }

    [Fact]
    public void SixelFragment_Cropped_ReencodesPayload()
    {
        var sixelRef = CreateTestSixel(100, 60);
        var fragment = new SixelFragment(sixelRef.Data, 0, 0, new PixelRect(10, 10, 50, 30));
        
        Assert.False(fragment.IsComplete);
        
        var payload = fragment.GetPayload();
        Assert.NotNull(payload);
        Assert.NotEqual(sixelRef.Data.Payload, payload);
        Assert.Contains("\x1bP", payload); // Valid sixel start
    }

    [Fact]
    public void SixelFragment_GetCellSpan_CalculatesCorrectly()
    {
        var sixelRef = CreateTestSixel(100, 60);
        var fragment = new SixelFragment(sixelRef.Data, 0, 0, new PixelRect(0, 0, 55, 35));
        var metrics = new CellMetrics(10, 20);
        
        var (width, height) = fragment.GetCellSpan(metrics);
        
        Assert.Equal(6, width);  // ceil(55/10)
        Assert.Equal(2, height); // ceil(35/20)
    }

    #endregion

    #region Helper Methods

    private TrackedObject<SixelData> CreateTestSixel(int pixelWidth, int pixelHeight)
    {
        // Create a simple test pattern
        var buffer = new SixelPixelBuffer(pixelWidth, pixelHeight);
        for (var y = 0; y < pixelHeight; y++)
            for (var x = 0; x < pixelWidth; x++)
                buffer[x, y] = Rgba32.FromRgb((byte)(x % 256), (byte)(y % 256), 128);
        
        var payload = SixelEncoder.Encode(buffer);
        
        // Use the store to create a tracked object
        return _store.GetOrCreateSixel(payload, 
            (pixelWidth + 9) / 10,   // Approximate cell width
            (pixelHeight + 19) / 20); // Approximate cell height
    }

    #endregion
}
