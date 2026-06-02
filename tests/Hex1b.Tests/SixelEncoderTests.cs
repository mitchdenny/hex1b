using Hex1b;
using Hex1b.Automation;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for <see cref="SixelEncoder"/> and round-trip verification with <see cref="SixelDecoder"/>.
/// </summary>
[TestClass]
public class SixelEncoderTests
{
    #region T1: Encoder/Decoder Round-Trip Tests

    [TestMethod]
    public void T1_1_SolidColor_RoundTrip()
    {
        // Single solid red color
        var buffer = CreateSolidBuffer(10, 12, Rgba32.FromRgb(255, 0, 0));
        
        var encoded = SixelEncoder.Encode(buffer);
        var decoded = SixelDecoder.Decode(encoded);
        
        Assert.IsNotNull(decoded);
        Assert.AreEqual(10, decoded.Width);
        Assert.AreEqual(12, decoded.Height);
        
        // Check center pixel is red (allowing for quantization)
        AssertPixelSimilar(decoded, 5, 6, 255, 0, 0);
    }

    [TestMethod]
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
        
        Assert.IsNotNull(decoded);
        AssertPixelSimilar(decoded, 2, 6, 255, 0, 0);   // Left half - red
        AssertPixelSimilar(decoded, 15, 6, 0, 0, 255);  // Right half - blue
    }

    [TestMethod]
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
        
        Assert.IsNotNull(decoded);
        Assert.AreEqual(16, decoded.Width);
        // Height may be padded to band boundary (multiple of 6)
        Assert.IsTrue(decoded.Height >= 16);
    }

    [TestMethod]
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
        
        Assert.IsNotNull(decoded);
        
        // Left should be dark, right should be bright
        var leftPixel = GetPixel(decoded, 5, 6);
        var rightPixel = GetPixel(decoded, 94, 6);
        Assert.IsTrue(rightPixel.r > leftPixel.r, "Gradient should go from dark to bright");
    }

    [TestMethod]
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
        
        Assert.IsNotNull(decoded);
        
        // Check opaque pixel
        var opaquePixel = GetPixel(decoded, 0, 0);
        Assert.AreEqual(255, opaquePixel.a);
        
        // Check transparent pixel
        var transparentPixel = GetPixel(decoded, 1, 0);
        Assert.AreEqual(0, transparentPixel.a);
    }

    [TestMethod]
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
        
        Assert.IsNotNull(decoded);
        
        // Top should be green
        AssertPixelSimilar(decoded, 10, 2, 0, 255, 0);
        
        // Bottom should be transparent
        var bottomPixel = GetPixel(decoded, 10, 10);
        Assert.AreEqual(0, bottomPixel.a);
    }

    [TestMethod]
    public void T1_7_SinglePixel()
    {
        var buffer = CreateSolidBuffer(1, 1, Rgba32.FromRgb(128, 128, 128));
        
        var encoded = SixelEncoder.Encode(buffer);
        var decoded = SixelDecoder.Decode(encoded);
        
        Assert.IsNotNull(decoded);
        Assert.AreEqual(1, decoded.Width);
        // Height will be padded to band boundary (6)
        Assert.IsTrue(decoded.Height >= 1);
    }

    [TestMethod]
    public void T1_8_SingleRow()
    {
        var buffer = new SixelPixelBuffer(50, 1);
        for (var x = 0; x < 50; x++)
            buffer[x, 0] = Rgba32.FromRgb(255, 255, 0);
        
        var encoded = SixelEncoder.Encode(buffer);
        var decoded = SixelDecoder.Decode(encoded);
        
        Assert.IsNotNull(decoded);
        Assert.AreEqual(50, decoded.Width);
    }

    [TestMethod]
    public void T1_9_SingleColumn()
    {
        var buffer = new SixelPixelBuffer(1, 50);
        for (var y = 0; y < 50; y++)
            buffer[0, y] = Rgba32.FromRgb(0, 255, 255);
        
        var encoded = SixelEncoder.Encode(buffer);
        var decoded = SixelDecoder.Decode(encoded);
        
        Assert.IsNotNull(decoded);
        Assert.IsTrue(decoded.Height >= 50);
    }

    [TestMethod]
    public void T1_10_LargeImage()
    {
        // 100x100 should work (though we won't test 1000x1000 for speed)
        var buffer = CreateSolidBuffer(100, 100, Rgba32.FromRgb(100, 150, 200));
        
        var encoded = SixelEncoder.Encode(buffer);
        var decoded = SixelDecoder.Decode(encoded);
        
        Assert.IsNotNull(decoded);
        Assert.AreEqual(100, decoded.Width);
        // Height may be padded to band boundary (multiple of 6)
        Assert.IsTrue(decoded.Height >= 100);
    }

    [TestMethod]
    public void T1_11_NonMultipleOf6Height()
    {
        // Height 10 is not a multiple of 6 (band size)
        var buffer = CreateSolidBuffer(20, 10, Rgba32.FromRgb(200, 100, 50));
        
        var encoded = SixelEncoder.Encode(buffer);
        var decoded = SixelDecoder.Decode(encoded);
        
        Assert.IsNotNull(decoded);
        Assert.AreEqual(20, decoded.Width);
        Assert.IsTrue(decoded.Height >= 10);
    }

    [TestMethod]
    public void T1_12_WideImage()
    {
        // Very wide image
        var buffer = CreateSolidBuffer(500, 6, Rgba32.FromRgb(50, 100, 150));
        
        var encoded = SixelEncoder.Encode(buffer);
        var decoded = SixelDecoder.Decode(encoded);
        
        Assert.IsNotNull(decoded);
        Assert.AreEqual(500, decoded.Width);
    }

    #endregion

    #region Encoder Format Tests

    [TestMethod]
    public void Encode_ContainsDcsHeader()
    {
        var buffer = CreateSolidBuffer(10, 6, Rgba32.FromRgb(255, 0, 0));
        var encoded = SixelEncoder.Encode(buffer);
        
        Assert.StartsWith("\x1bP", encoded); // DCS
        Assert.Contains("q", encoded);        // Sixel introducer
    }

    [TestMethod]
    public void Encode_ContainsStringTerminator()
    {
        var buffer = CreateSolidBuffer(10, 6, Rgba32.FromRgb(255, 0, 0));
        var encoded = SixelEncoder.Encode(buffer);
        
        Assert.EndsWith("\x1b\\", encoded); // ST
    }

    [TestMethod]
    public void Encode_ContainsColorDefinitions()
    {
        var buffer = CreateSolidBuffer(10, 6, Rgba32.FromRgb(255, 0, 0));
        var encoded = SixelEncoder.Encode(buffer);
        
        // Should contain color definition: #<index>;2;<r>;<g>;<b>
        Assert.Contains("#0;2;", encoded);
    }

    [TestMethod]
    public void Encode_ContainsRasterAttributes()
    {
        var buffer = CreateSolidBuffer(20, 12, Rgba32.FromRgb(0, 255, 0));
        var encoded = SixelEncoder.Encode(buffer);
        
        // Should contain raster attributes: "1;1;<width>;<height>
        Assert.Contains("\"1;1;20;12", encoded);
    }

    [TestMethod]
    public void Encode_UsesRleForLongRuns()
    {
        // Solid color should produce RLE sequences
        var buffer = CreateSolidBuffer(100, 6, Rgba32.FromRgb(255, 255, 255));
        var encoded = SixelEncoder.Encode(buffer);
        
        // Should contain RLE: !<count><char>
        Assert.Contains("!", encoded);
    }

    [TestMethod]
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

    [TestMethod]
    public void SixelPixelBuffer_Crop_ReturnsCorrectRegion()
    {
        var buffer = new SixelPixelBuffer(20, 20);
        
        // Mark a specific region
        for (var y = 5; y < 15; y++)
            for (var x = 5; x < 15; x++)
                buffer[x, y] = Rgba32.FromRgb(255, 0, 0);
        
        var cropped = buffer.Crop(5, 5, 10, 10);
        
        Assert.AreEqual(10, cropped.Width);
        Assert.AreEqual(10, cropped.Height);
        Assert.AreEqual(Rgba32.FromRgb(255, 0, 0), cropped[0, 0]);
        Assert.AreEqual(Rgba32.FromRgb(255, 0, 0), cropped[9, 9]);
    }

    [TestMethod]
    public void SixelPixelBuffer_Crop_ClampsToValidBounds()
    {
        var buffer = new SixelPixelBuffer(10, 10);
        for (var y = 0; y < 10; y++)
            for (var x = 0; x < 10; x++)
                buffer[x, y] = Rgba32.FromRgb(0, 255, 0);
        
        // Request region that extends past bounds
        var cropped = buffer.Crop(5, 5, 20, 20);
        
        Assert.AreEqual(5, cropped.Width);
        Assert.AreEqual(5, cropped.Height);
    }

    [TestMethod]
    public void SixelPixelBuffer_GetPixelOrTransparent_ReturnsTransparentForOutOfBounds()
    {
        var buffer = new SixelPixelBuffer(10, 10);
        buffer[5, 5] = Rgba32.FromRgb(255, 0, 0);
        
        Assert.AreEqual(Rgba32.Transparent, buffer.GetPixelOrTransparent(-1, 5));
        Assert.AreEqual(Rgba32.Transparent, buffer.GetPixelOrTransparent(5, -1));
        Assert.AreEqual(Rgba32.Transparent, buffer.GetPixelOrTransparent(10, 5));
        Assert.AreEqual(Rgba32.Transparent, buffer.GetPixelOrTransparent(5, 10));
        Assert.AreEqual(Rgba32.FromRgb(255, 0, 0), buffer.GetPixelOrTransparent(5, 5));
    }

    #endregion

    #region T2: PixelRect Tests

    [TestMethod]
    public void T2_1_PixelRect_Properties()
    {
        var rect = new PixelRect(10, 20, 30, 40);
        
        Assert.AreEqual(10, rect.X);
        Assert.AreEqual(20, rect.Y);
        Assert.AreEqual(30, rect.Width);
        Assert.AreEqual(40, rect.Height);
        Assert.AreEqual(40, rect.Right);
        Assert.AreEqual(60, rect.Bottom);
        Assert.AreEqual(1200, rect.Area);
        Assert.IsFalse(rect.IsEmpty);
    }

    [TestMethod]
    public void T2_2_PixelRect_IsEmpty()
    {
        Assert.IsTrue(new PixelRect(0, 0, 0, 10).IsEmpty);
        Assert.IsTrue(new PixelRect(0, 0, 10, 0).IsEmpty);
        Assert.IsTrue(new PixelRect(0, 0, -5, 10).IsEmpty);
        Assert.IsFalse(new PixelRect(0, 0, 1, 1).IsEmpty);
    }

    [TestMethod]
    public void T2_3_PixelRect_Intersect_Overlapping()
    {
        var a = new PixelRect(0, 0, 20, 20);
        var b = new PixelRect(10, 10, 20, 20);
        
        var intersection = a.Intersect(b);
        
        Assert.AreEqual(10, intersection.X);
        Assert.AreEqual(10, intersection.Y);
        Assert.AreEqual(10, intersection.Width);
        Assert.AreEqual(10, intersection.Height);
    }

    [TestMethod]
    public void T2_4_PixelRect_Intersect_NoOverlap()
    {
        var a = new PixelRect(0, 0, 10, 10);
        var b = new PixelRect(20, 20, 10, 10);
        
        var intersection = a.Intersect(b);
        
        Assert.IsTrue(intersection.IsEmpty);
    }

    [TestMethod]
    public void T2_5_PixelRect_Intersect_Contained()
    {
        var outer = new PixelRect(0, 0, 100, 100);
        var inner = new PixelRect(25, 25, 50, 50);
        
        var intersection = outer.Intersect(inner);
        
        Assert.AreEqual(inner, intersection);
    }

    [TestMethod]
    public void T2_6_PixelRect_Contains_Point()
    {
        var rect = new PixelRect(10, 10, 20, 20);
        
        Assert.IsTrue(rect.Contains(10, 10));   // Top-left corner
        Assert.IsTrue(rect.Contains(29, 29));   // Bottom-right (exclusive edge -1)
        Assert.IsTrue(rect.Contains(20, 20));   // Center
        Assert.IsFalse(rect.Contains(9, 10));   // Left of
        Assert.IsFalse(rect.Contains(30, 10));  // Right edge (exclusive)
        Assert.IsFalse(rect.Contains(10, 30));  // Bottom edge (exclusive)
    }

    [TestMethod]
    public void T2_7_PixelRect_Contains_Rect()
    {
        var outer = new PixelRect(0, 0, 100, 100);
        var inner = new PixelRect(25, 25, 50, 50);
        var partial = new PixelRect(50, 50, 100, 100);
        
        Assert.IsTrue(outer.Contains(inner));
        Assert.IsFalse(outer.Contains(partial));
        Assert.IsFalse(inner.Contains(outer));
    }

    [TestMethod]
    public void T2_8_PixelRect_Subtract_NoOverlap()
    {
        var rect = new PixelRect(0, 0, 10, 10);
        var hole = new PixelRect(20, 20, 10, 10);
        
        var fragments = rect.Subtract(hole);
        
        TestSeq.Single(fragments);
        Assert.AreEqual(rect, fragments[0]);
    }

    [TestMethod]
    public void T2_9_PixelRect_Subtract_CenterHole()
    {
        // 10x10 rectangle with 4x4 hole in center
        var rect = new PixelRect(0, 0, 10, 10);
        var hole = new PixelRect(3, 3, 4, 4);
        
        var fragments = rect.Subtract(hole);
        
        // Should produce 4 fragments: top, bottom, left, right
        Assert.AreEqual(4, fragments.Count);
        
        // Verify total area equals original minus hole
        var totalArea = fragments.Sum(f => f.Area);
        Assert.AreEqual(rect.Area - hole.Area, totalArea);
    }

    [TestMethod]
    public void T2_10_PixelRect_Subtract_EdgeHole()
    {
        var rect = new PixelRect(0, 0, 10, 10);
        var hole = new PixelRect(0, 0, 5, 5); // Top-left corner
        
        var fragments = rect.Subtract(hole);
        
        // Should produce 2 fragments: bottom and right
        Assert.AreEqual(2, fragments.Count);
        var totalArea = fragments.Sum(f => f.Area);
        Assert.AreEqual(rect.Area - hole.Area, totalArea);
    }

    [TestMethod]
    public void T2_11_PixelRect_Subtract_FullyCovered()
    {
        var rect = new PixelRect(5, 5, 10, 10);
        var hole = new PixelRect(0, 0, 20, 20); // Covers entire rect
        
        var fragments = rect.Subtract(hole);
        
        Assert.IsEmpty(fragments);
    }

    #endregion

    #region T3: Fragment and Visibility Tests

    [TestMethod]
    public void T3_1_Fragment_SingleRegion()
    {
        var buffer = new SixelPixelBuffer(20, 20);
        for (var y = 0; y < 20; y++)
            for (var x = 0; x < 20; x++)
                buffer[x, y] = Rgba32.FromRgb((byte)(x * 10), (byte)(y * 10), 0);
        
        var regions = new[] { new PixelRect(5, 5, 10, 10) };
        var fragments = buffer.Fragment(regions);
        
        TestSeq.Single(fragments);
        Assert.AreEqual(new PixelRect(5, 5, 10, 10), fragments[0].Region);
        Assert.AreEqual(10, fragments[0].Buffer.Width);
        Assert.AreEqual(10, fragments[0].Buffer.Height);
        // First pixel should be from (5,5) in original
        Assert.AreEqual(Rgba32.FromRgb(50, 50, 0), fragments[0].Buffer[0, 0]);
    }

    [TestMethod]
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
        
        Assert.AreEqual(4, fragments.Count);
        TestSeq.All(fragments, f =>
        {
            Assert.AreEqual(10, f.Buffer.Width);
            Assert.AreEqual(10, f.Buffer.Height);
        });
    }

    [TestMethod]
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
        
        Assert.AreEqual(2, fragments.Count);
    }

    [TestMethod]
    public void T3_4_Fragment_RegionClampedToBounds()
    {
        var buffer = new SixelPixelBuffer(10, 10);
        
        var regions = new[] { new PixelRect(5, 5, 100, 100) }; // Extends way past bounds
        var fragments = buffer.Fragment(regions);
        
        TestSeq.Single(fragments);
        Assert.AreEqual(new PixelRect(5, 5, 5, 5), fragments[0].Region);
        Assert.AreEqual(5, fragments[0].Buffer.Width);
        Assert.AreEqual(5, fragments[0].Buffer.Height);
    }

    [TestMethod]
    public void T3_5_ComputeVisibleRegions_NoOcclusions()
    {
        var buffer = new SixelPixelBuffer(100, 60);
        
        var visible = buffer.ComputeVisibleRegions(Array.Empty<PixelRect>());
        
        TestSeq.Single(visible);
        Assert.AreEqual(new PixelRect(0, 0, 100, 60), visible[0]);
    }

    [TestMethod]
    public void T3_6_ComputeVisibleRegions_CenterOcclusion()
    {
        var buffer = new SixelPixelBuffer(100, 60);
        var occlusion = new PixelRect(25, 15, 50, 30); // Center hole
        
        var visible = buffer.ComputeVisibleRegions(new[] { occlusion });
        
        Assert.AreEqual(4, visible.Count);
        var totalArea = visible.Sum(r => r.Area);
        Assert.AreEqual(100 * 60 - 50 * 30, totalArea);
    }

    [TestMethod]
    public void T3_7_ComputeVisibleRegions_FullyOccluded()
    {
        var buffer = new SixelPixelBuffer(50, 50);
        var occlusion = new PixelRect(0, 0, 100, 100); // Covers everything
        
        var visible = buffer.ComputeVisibleRegions(new[] { occlusion });
        
        Assert.IsEmpty(visible);
    }

    [TestMethod]
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
        Assert.IsTrue(visible.Count >= 2);
        // No visible region should overlap with occlusions
        foreach (var region in visible)
        {
            foreach (var occ in occlusions)
            {
                Assert.IsTrue(region.Intersect(occ).IsEmpty);
            }
        }
    }

    [TestMethod]
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
            
            Assert.IsNotNull(decoded);
            Assert.AreEqual(fragmentBuffer.Width, decoded.Width);
            Assert.IsTrue(decoded.Height >= fragmentBuffer.Height);
        }
    }

    #endregion

    #region T4: CellMetrics Tests

    [TestMethod]
    public void T4_1_CellMetrics_Default()
    {
        var metrics = CellMetrics.Default;
        
        Assert.AreEqual(10, metrics.PixelWidth);
        Assert.AreEqual(20, metrics.PixelHeight);
    }

    [TestMethod]
    public void T4_2_CellMetrics_CellToPixel()
    {
        var metrics = new CellMetrics(10, 20);
        
        var pixelRect = metrics.CellToPixel(5, 3, 10, 5);
        
        Assert.AreEqual(50, pixelRect.X);    // 5 * 10
        Assert.AreEqual(60, pixelRect.Y);    // 3 * 20
        Assert.AreEqual(100, pixelRect.Width);  // 10 * 10
        Assert.AreEqual(100, pixelRect.Height); // 5 * 20
    }

    [TestMethod]
    public void T4_3_CellMetrics_PixelToCellSpan()
    {
        var metrics = new CellMetrics(10, 20);
        
        // Exact multiple
        var (w1, h1) = metrics.PixelToCellSpan(100, 60);
        Assert.AreEqual(10, w1);
        Assert.AreEqual(3, h1);
        
        // Rounds up
        var (w2, h2) = metrics.PixelToCellSpan(101, 61);
        Assert.AreEqual(11, w2);
        Assert.AreEqual(4, h2);
    }

    [TestMethod]
    public void T4_4_CellMetrics_PixelToCell()
    {
        var metrics = new CellMetrics(10, 20);
        
        var cellRect = metrics.PixelToCell(new PixelRect(50, 60, 100, 100));
        
        Assert.AreEqual(5, cellRect.X);   // 50 / 10
        Assert.AreEqual(3, cellRect.Y);   // 60 / 20
        Assert.AreEqual(10, cellRect.Width);  // 100 / 10
        Assert.AreEqual(5, cellRect.Height);  // 100 / 20
    }

    [TestMethod]
    public void T4_5_CellMetrics_RoundTrip()
    {
        var metrics = new CellMetrics(8, 16);
        
        // Cell -> Pixel -> Cell should give same cell coordinates
        var originalCell = new Hex1b.Layout.Rect(3, 5, 10, 8);
        var pixel = metrics.CellToPixel(originalCell);
        var backToCell = metrics.PixelToCell(pixel);
        
        Assert.AreEqual(originalCell.X, backToCell.X);
        Assert.AreEqual(originalCell.Y, backToCell.Y);
        Assert.AreEqual(originalCell.Width, backToCell.Width);
        Assert.AreEqual(originalCell.Height, backToCell.Height);
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
        
        Assert.IsTrue(a > 0, $"Pixel at ({x}, {y}) should not be transparent");
        Assert.IsTrue(Math.Abs(r - expectedR) <= tolerance, $"Red at ({x}, {y}): expected ~{expectedR}, got {r}");
        Assert.IsTrue(Math.Abs(g - expectedG) <= tolerance, $"Green at ({x}, {y}): expected ~{expectedG}, got {g}");
        Assert.IsTrue(Math.Abs(b - expectedB) <= tolerance, $"Blue at ({x}, {y}): expected ~{expectedB}, got {b}");
    }

    #endregion
}

/// <summary>
/// Tests for sixel visibility and fragment generation.
/// </summary>
[TestClass]
public class SixelVisibilityTests
{
    private readonly TrackedObjectStore _store = new();

    #region SixelVisibility Tests

    [TestMethod]
    public void SixelVisibility_InitiallyFullyVisible()
    {
        var sixelRef = CreateTestSixel(100, 60); // 100x60 pixels = 10x3 cells at 10x20 metrics
        var visibility = new SixelVisibility(sixelRef, 5, 5, 0);
        
        Assert.IsTrue(visibility.IsFullyVisible);
        Assert.IsFalse(visibility.IsFullyOccluded);
        Assert.IsFalse(visibility.IsFragmented);
        TestSeq.Single(visibility.VisibleRegions);
    }

    [TestMethod]
    public void SixelVisibility_ApplyOcclusion_PartiallyOccludes()
    {
        var sixelRef = CreateTestSixel(100, 60);
        var visibility = new SixelVisibility(sixelRef, 0, 0, 0);
        var metrics = new CellMetrics(10, 20);
        
        // Occlude center 2x1 cells (cells 4-5, row 1)
        var occlusion = new Hex1b.Layout.Rect(4, 1, 2, 1);
        visibility.ApplyOcclusion(occlusion, metrics);
        
        Assert.IsFalse(visibility.IsFullyVisible);
        Assert.IsFalse(visibility.IsFullyOccluded);
        // Should have 4 fragments: top, bottom, left, right of occlusion
        Assert.AreEqual(4, visibility.VisibleRegions.Count);
    }

    [TestMethod]
    public void SixelVisibility_ApplyOcclusion_FullyOccludes()
    {
        var sixelRef = CreateTestSixel(100, 60);
        var visibility = new SixelVisibility(sixelRef, 0, 0, 0);
        var metrics = new CellMetrics(10, 20);
        
        // Occlude entire sixel (10x3 cells)
        var occlusion = new Hex1b.Layout.Rect(0, 0, 10, 3);
        visibility.ApplyOcclusion(occlusion, metrics);
        
        Assert.IsTrue(visibility.IsFullyOccluded);
        Assert.IsEmpty(visibility.VisibleRegions);
    }

    [TestMethod]
    public void SixelVisibility_ApplyOcclusion_NoOverlap_NoChange()
    {
        var sixelRef = CreateTestSixel(100, 60);
        var visibility = new SixelVisibility(sixelRef, 0, 0, 0);
        var metrics = new CellMetrics(10, 20);
        
        // Occlude area outside sixel
        var occlusion = new Hex1b.Layout.Rect(20, 20, 5, 5);
        visibility.ApplyOcclusion(occlusion, metrics);
        
        Assert.IsTrue(visibility.IsFullyVisible);
        TestSeq.Single(visibility.VisibleRegions);
    }

    [TestMethod]
    public void SixelVisibility_GenerateFragments_FullyVisible_SingleFragment()
    {
        var sixelRef = CreateTestSixel(100, 60);
        var visibility = new SixelVisibility(sixelRef, 5, 3, 0);
        var metrics = new CellMetrics(10, 20);
        
        var fragments = visibility.GenerateFragments(metrics);
        
        TestSeq.Single(fragments);
        Assert.AreEqual((5, 3), fragments[0].CellPosition);
        Assert.IsTrue(fragments[0].IsComplete);
    }

    [TestMethod]
    public void SixelVisibility_GenerateFragments_Occluded_MultipleFragments()
    {
        var sixelRef = CreateTestSixel(100, 60);
        var visibility = new SixelVisibility(sixelRef, 0, 0, 0);
        var metrics = new CellMetrics(10, 20);
        
        // Occlude center
        var occlusion = new Hex1b.Layout.Rect(4, 1, 2, 1);
        visibility.ApplyOcclusion(occlusion, metrics);
        
        var fragments = visibility.GenerateFragments(metrics);
        
        Assert.IsTrue(fragments.Count >= 2);
        TestSeq.All(fragments, f => Assert.IsFalse(f.IsComplete));
    }

    #endregion

    #region CompositeSurface.GetSixelFragments Tests

    [TestMethod]
    public void GetSixelFragments_NoSixels_ReturnsEmpty()
    {
        var composite = new CompositeSurface(80, 24);
        var layer = new Surface(40, 12);
        layer.WriteText(0, 0, "Hello");
        composite.AddLayer(layer, 0, 0);
        
        var fragments = composite.GetSixelFragments();
        
        Assert.IsEmpty(fragments);
    }

    [TestMethod]
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
        
        TestSeq.Single(fragments);
        Assert.AreEqual((5, 3), fragments[0].CellPosition);
    }

    [TestMethod]
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
        Assert.IsTrue(fragments.Count >= 2);
    }

    [TestMethod]
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
        
        Assert.IsEmpty(fragments);
    }

    [TestMethod]
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
        
        Assert.AreEqual(2, fragments.Count);
    }

    #endregion

    #region SixelFragment Tests

    [TestMethod]
    public void SixelFragment_Complete_ReturnsOriginalPayload()
    {
        var sixelRef = CreateTestSixel(100, 60);
        var fragment = SixelFragment.Complete(sixelRef.Data, 0, 0);
        
        Assert.IsTrue(fragment.IsComplete);
        Assert.AreEqual(sixelRef.Data.Payload, fragment.GetPayload());
    }

    [TestMethod]
    public void SixelFragment_Cropped_ReencodesPayload()
    {
        var sixelRef = CreateTestSixel(100, 60);
        var fragment = new SixelFragment(sixelRef.Data, 0, 0, new PixelRect(10, 10, 50, 30));
        
        Assert.IsFalse(fragment.IsComplete);
        
        var payload = fragment.GetPayload();
        Assert.IsNotNull(payload);
        Assert.AreNotEqual(sixelRef.Data.Payload, payload);
        Assert.Contains("\x1bP", payload); // Valid sixel start
    }

    [TestMethod]
    public void SixelFragment_GetCellSpan_CalculatesCorrectly()
    {
        var sixelRef = CreateTestSixel(100, 60);
        var fragment = new SixelFragment(sixelRef.Data, 0, 0, new PixelRect(0, 0, 55, 35));
        var metrics = new CellMetrics(10, 20);
        
        var (width, height) = fragment.GetCellSpan(metrics);
        
        Assert.AreEqual(6, width);  // ceil(55/10)
        Assert.AreEqual(2, height); // ceil(35/20)
    }

    #endregion

    #region T5: Computed Cell Sixel Access Tests

    [TestMethod]
    public void T5_1_HasSixelBelow_ReturnsTrueWhenSixelPresent()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(40, 20, metrics);
        
        // Layer 0: Background with sixel
        var background = new Surface(40, 20, metrics);
        var sixelRef = CreateTestSixel(100, 60); // 10x3 cells
        background[5, 5] = new SurfaceCell(" ", null, null, Sixel: sixelRef);
        composite.AddLayer(background, 0, 0);
        
        // Layer 1: Computed layer that checks for sixel
        var results = new List<bool>();
        composite.AddComputedLayer(40, 20, ctx =>
        {
            if (ctx.X == 5 && ctx.Y == 5) // At sixel anchor
                results.Add(ctx.HasSixelBelow());
            if (ctx.X == 8 && ctx.Y == 6) // Inside sixel (5+3, 5+1)
                results.Add(ctx.HasSixelBelow());
            return SurfaceCells.Empty;
        }, 0, 0);
        
        // Force computation
        _ = composite.GetCell(5, 5);
        _ = composite.GetCell(8, 6);
        
        Assert.Contains(true, results);
    }

    [TestMethod]
    public void T5_2_HasSixelBelow_ReturnsFalseWhenNoSixel()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(40, 20, metrics);
        
        // Layer 0: Background without sixels
        var background = new Surface(40, 20, metrics);
        background.Fill(new Hex1b.Layout.Rect(0, 0, 40, 20), 
            new SurfaceCell(" ", null, Hex1bColor.Blue));
        composite.AddLayer(background, 0, 0);
        
        // Layer 1: Computed layer
        bool? result = null;
        composite.AddComputedLayer(40, 20, ctx =>
        {
            if (ctx.X == 10 && ctx.Y == 10)
                result = ctx.HasSixelBelow();
            return SurfaceCells.Empty;
        }, 0, 0);
        
        _ = composite.GetCell(10, 10);
        
        Assert.IsNotNull(result);
        Assert.IsFalse(result.Value);
    }

    [TestMethod]
    public void T5_3_GetSixelBelow_ReturnsValidPixelData()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(40, 20, metrics);
        
        // Create sixel with known color pattern
        var buffer = new SixelPixelBuffer(100, 60);
        for (var y = 0; y < 60; y++)
            for (var x = 0; x < 100; x++)
                buffer[x, y] = new Rgba32(255, 0, 0, 255); // Solid red
        
        var payload = SixelEncoder.Encode(buffer);
        var sixelRef = _store.GetOrCreateSixel(payload, 10, 3);
        
        var background = new Surface(40, 20, metrics);
        background[5, 5] = new SurfaceCell(" ", null, null, Sixel: sixelRef);
        composite.AddLayer(background, 0, 0);
        
        SixelPixelAccess? access = null;
        composite.AddComputedLayer(40, 20, ctx =>
        {
            if (ctx.X == 5 && ctx.Y == 5)
                access = ctx.GetSixelBelow();
            return SurfaceCells.Empty;
        }, 0, 0);
        
        _ = composite.GetCell(5, 5);
        
        Assert.IsNotNull(access);
        Assert.IsTrue(access.Value.IsValid);
        Assert.AreEqual(10, access.Value.PixelWidth);
        Assert.AreEqual(20, access.Value.PixelHeight);
        
        // Check pixel value (should be red, with quantization tolerance)
        var pixel = access.Value.GetPixel(0, 0);
        Assert.IsTrue(pixel.R > 240, $"Expected R ~255, got {pixel.R}"); // Allow sixel quantization error
        Assert.IsTrue(pixel.G < 15, $"Expected G ~0, got {pixel.G}");
        Assert.IsTrue(pixel.B < 15, $"Expected B ~0, got {pixel.B}");
    }

    [TestMethod]
    public void T5_4_PixelCoordinatesMapCorrectlyToCellPosition()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(40, 20, metrics);
        
        // Create sixel with gradient
        var buffer = new SixelPixelBuffer(100, 60);
        for (var y = 0; y < 60; y++)
            for (var x = 0; x < 100; x++)
                buffer[x, y] = new Rgba32((byte)x, (byte)y, 0, 255);
        
        var payload = SixelEncoder.Encode(buffer);
        var sixelRef = _store.GetOrCreateSixel(payload, 10, 3);
        
        var background = new Surface(40, 20, metrics);
        background[0, 0] = new SurfaceCell(" ", null, null, Sixel: sixelRef);
        composite.AddLayer(background, 0, 0);
        
        // Check cell (2, 1) - should access pixels starting at (20, 20)
        SixelPixelAccess? access = null;
        composite.AddComputedLayer(40, 20, ctx =>
        {
            if (ctx.X == 2 && ctx.Y == 1)
                access = ctx.GetSixelBelow();
            return SurfaceCells.Empty;
        }, 0, 0);
        
        _ = composite.GetCell(2, 1);
        
        Assert.IsNotNull(access);
        Assert.IsTrue(access.Value.IsValid);
        
        // Pixel at (0,0) in cell (2,1) should be global pixel (20, 20)
        var pixel = access.Value.GetPixel(0, 0);
        // Due to sixel color quantization, values may differ slightly
        // Original would be R=20, G=20
        Assert.IsTrue(pixel.R > 10 && pixel.R < 30, $"Expected R ~20, got {pixel.R}");
        Assert.IsTrue(pixel.G > 10 && pixel.G < 30, $"Expected G ~20, got {pixel.G}");
    }

    [TestMethod]
    public void T5_5_SixelPixelAccess_WithTint_AppliesTintCorrectly()
    {
        var buffer = new SixelPixelBuffer(10, 20);
        for (var y = 0; y < 20; y++)
            for (var x = 0; x < 10; x++)
                buffer[x, y] = new Rgba32(100, 100, 100, 255); // Gray
        
        var metrics = new CellMetrics(10, 20);
        var access = new SixelPixelAccess(buffer, 0, 0, metrics);
        
        var tintedBuffer = access.WithTint(new Rgba32(255, 0, 0, 255), 0.5f);
        
        Assert.IsNotNull(tintedBuffer);
        
        // Result should be blend: 100 * 0.5 + 255 * 0.5 = 177.5 for R
        var resultPixel = tintedBuffer[5, 10];
        Assert.IsTrue(resultPixel.R > 170 && resultPixel.R < 185, $"Expected R ~177, got {resultPixel.R}");
        Assert.IsTrue(resultPixel.G > 45 && resultPixel.G < 55, $"Expected G ~50, got {resultPixel.G}");
        Assert.IsTrue(resultPixel.B > 45 && resultPixel.B < 55, $"Expected B ~50, got {resultPixel.B}");
    }

    [TestMethod]
    public void T5_6_SixelPixelAccess_WithBrightness_AdjustsValues()
    {
        var buffer = new SixelPixelBuffer(10, 20);
        for (var y = 0; y < 20; y++)
            for (var x = 0; x < 10; x++)
                buffer[x, y] = new Rgba32(100, 100, 100, 255);
        
        var metrics = new CellMetrics(10, 20);
        var access = new SixelPixelAccess(buffer, 0, 0, metrics);
        
        // Reduce brightness by half
        var dimmedBuffer = access.WithBrightness(0.5f);
        
        Assert.IsNotNull(dimmedBuffer);
        
        var resultPixel = dimmedBuffer[5, 10];
        Assert.AreEqual(50, resultPixel.R);
        Assert.AreEqual(50, resultPixel.G);
        Assert.AreEqual(50, resultPixel.B);
    }

    [TestMethod]
    public void T5_7_GetSixelBelowAt_ReturnsDataForDifferentPosition()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(40, 20, metrics);
        
        // Create sixel at position (0, 0)
        var buffer = new SixelPixelBuffer(100, 60);
        for (var y = 0; y < 60; y++)
            for (var x = 0; x < 100; x++)
                buffer[x, y] = new Rgba32(200, 100, 50, 255);
        
        var payload = SixelEncoder.Encode(buffer);
        var sixelRef = _store.GetOrCreateSixel(payload, 10, 3);
        
        var background = new Surface(40, 20, metrics);
        background[0, 0] = new SurfaceCell(" ", null, null, Sixel: sixelRef);
        composite.AddLayer(background, 0, 0);
        
        // Query from computed layer at different position
        SixelPixelAccess? access = null;
        composite.AddComputedLayer(40, 20, ctx =>
        {
            if (ctx.X == 20 && ctx.Y == 10) // Far from sixel
                access = ctx.GetSixelBelowAt(3, 1); // Query sixel at (3, 1)
            return SurfaceCells.Empty;
        }, 0, 0);
        
        _ = composite.GetCell(20, 10);
        
        Assert.IsNotNull(access);
        Assert.IsTrue(access.Value.IsValid);
    }

    [TestMethod]
    public void T5_8_SixelPixelAccess_TransparentPixels_HandledCorrectly()
    {
        var buffer = new SixelPixelBuffer(10, 20);
        // Half transparent, half opaque
        for (var y = 0; y < 20; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                if (x < 5)
                    buffer[x, y] = Rgba32.Transparent;
                else
                    buffer[x, y] = new Rgba32(255, 0, 0, 255);
            }
        }
        
        var metrics = new CellMetrics(10, 20);
        var access = new SixelPixelAccess(buffer, 0, 0, metrics);
        
        Assert.IsTrue(access.HasVisiblePixels());
        
        var transparentPixel = access.GetPixel(0, 0);
        Assert.AreEqual(0, transparentPixel.A);
        
        var opaquePixel = access.GetPixel(5, 0);
        Assert.AreEqual(255, opaquePixel.A);
    }

    [TestMethod]
    public void T5_9_SixelPixelAccess_OutOfBoundsPixel_ReturnsTransparent()
    {
        var buffer = new SixelPixelBuffer(10, 20);
        buffer[5, 10] = new Rgba32(255, 0, 0, 255);
        
        var metrics = new CellMetrics(10, 20);
        var access = new SixelPixelAccess(buffer, 0, 0, metrics);
        
        // Query beyond buffer bounds
        var pixel = access.GetPixel(100, 100);
        Assert.AreEqual(0, pixel.A);
    }

    [TestMethod]
    public void T5_10_GetSixelBelow_InvalidAccessor_WhenNoSixel()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(40, 20, metrics);
        
        var background = new Surface(40, 20, metrics);
        background.WriteText(0, 0, "No sixels here");
        composite.AddLayer(background, 0, 0);
        
        SixelPixelAccess access = default;
        composite.AddComputedLayer(40, 20, ctx =>
        {
            if (ctx.X == 5 && ctx.Y == 5)
                access = ctx.GetSixelBelow();
            return SurfaceCells.Empty;
        }, 0, 0);
        
        _ = composite.GetCell(5, 5);
        
        Assert.IsFalse(access.IsValid);
        
        // GetPixel on invalid accessor should return transparent
        var pixel = access.GetPixel(0, 0);
        Assert.AreEqual(0, pixel.A);
    }

    #endregion

    #region T6: Token Generation Sixel Tests

    [TestMethod]
    public void T6_1_SingleSixel_CorrectCursorPositionAndSequence()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(80, 24, metrics);
        
        var layer = new Surface(40, 12, metrics);
        var sixelRef = CreateTestSixel(50, 30); // 5x2 cells
        layer[5, 5] = new SurfaceCell(" ", null, null, Sixel: sixelRef);
        composite.AddLayer(layer, 0, 0);
        
        var tokens = SurfaceComparer.SixelFragmentsToTokens(composite);
        
        Assert.AreEqual(2, tokens.Count); // CursorPosition + sixel sequence
        TestSeq.IsType<CursorPositionToken>(tokens[0]);
        TestSeq.IsType<UnrecognizedSequenceToken>(tokens[1]); // Raw DCS sequence
        
        var cursorToken = (CursorPositionToken)tokens[0];
        Assert.AreEqual(6, cursorToken.Row);    // 1-based: 5 + 1
        Assert.AreEqual(6, cursorToken.Column); // 1-based: 5 + 1
        
        var sequenceToken = (UnrecognizedSequenceToken)tokens[1];
        Assert.StartsWith("\x1bP", sequenceToken.Sequence); // Payload already includes DCS wrapper
    }

    [TestMethod]
    public void T6_2_FragmentedSixel_MultipleCursorPositionsAndSequences()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(80, 24, metrics);
        
        // Large sixel
        var background = new Surface(40, 12, metrics);
        var sixelRef = CreateTestSixel(100, 60); // 10x3 cells
        background[0, 0] = new SurfaceCell(" ", null, null, Sixel: sixelRef);
        composite.AddLayer(background, 0, 0);
        
        // Occluding dialog in center
        var dialog = new Surface(4, 2, metrics);
        dialog.Fill(new Hex1b.Layout.Rect(0, 0, 4, 2), 
            new SurfaceCell(" ", null, Hex1bColor.Blue));
        composite.AddLayer(dialog, 3, 0);
        
        var tokens = SurfaceComparer.SixelFragmentsToTokens(composite);
        
        // Should have multiple fragments (each with cursor + sixel sequence)
        Assert.IsTrue(tokens.Count >= 4, $"Expected at least 4 tokens, got {tokens.Count}");
        
        // Verify alternating pattern: cursor, sixel, cursor, sixel, ...
        for (var i = 0; i < tokens.Count; i += 2)
        {
            TestSeq.IsType<CursorPositionToken>(tokens[i]);
            TestSeq.IsType<UnrecognizedSequenceToken>(tokens[i + 1]); // Raw DCS sequence
        }
    }

    [TestMethod]
    public void T6_3_SixelUnchanged_NotReEmittedWithDiff()
    {
        var metrics = new CellMetrics(10, 20);
        
        // Create previous surface with text only
        var previous = new Surface(80, 24, metrics);
        previous.WriteText(0, 0, "Hello");
        
        // Create composite with same text
        var composite = new CompositeSurface(80, 24, metrics);
        var layer = new Surface(80, 24, metrics);
        layer.WriteText(0, 0, "Hello");
        composite.AddLayer(layer, 0, 0);
        
        var tokens = SurfaceComparer.CompositeToTokens(composite, previous);
        
        // No sixels in either, so no sixel tokens
        TestSeq.All(tokens, t => Assert.IsNotInstanceOfType<DcsToken>(t));
    }

    [TestMethod]
    public void T6_4_SixelAndTextMixed_BothInOutput()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(80, 24, metrics);
        
        var layer = new Surface(40, 12, metrics);
        layer.WriteText(0, 0, "Title");
        layer[20, 5] = new SurfaceCell(" ", null, null, Sixel: CreateTestSixel(50, 30));
        composite.AddLayer(layer, 0, 0);
        
        var tokens = SurfaceComparer.CompositeToTokens(composite);
        
        // Should have text tokens
        var textTokens = tokens.OfType<TextToken>().ToList();
        Assert.IsTrue(textTokens.Any(t => t.Text == "T"));
        
        // Should have sixel token(s) - raw DCS sequence containing sixel data
        var sixelTokens = tokens.OfType<UnrecognizedSequenceToken>()
            .Where(t => t.Sequence.Contains("0;1;0q")) // Sixel header pattern
            .ToList();
        Assert.IsNotEmpty(sixelTokens);
    }

    [TestMethod]
    public void T6_5_MultipleSixelsAtDifferentPositions()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(80, 24, metrics);
        
        var layer = new Surface(80, 24, metrics);
        layer[0, 0] = new SurfaceCell(" ", null, null, Sixel: CreateTestSixel(30, 20));
        layer[40, 10] = new SurfaceCell(" ", null, null, Sixel: CreateTestSixel(30, 20));
        composite.AddLayer(layer, 0, 0);
        
        var tokens = SurfaceComparer.SixelFragmentsToTokens(composite);
        
        // Each sixel gets cursor + dcs
        Assert.AreEqual(4, tokens.Count);
        
        var cursor1 = (CursorPositionToken)tokens[0];
        var cursor2 = (CursorPositionToken)tokens[2];
        
        // Different positions
        Assert.AreNotEqual(cursor1.Row, cursor2.Row);
    }

    [TestMethod]
    public void T6_6_FullyOccludedSixel_NoTokensGenerated()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(80, 24, metrics);
        
        // Small sixel
        var background = new Surface(20, 10, metrics);
        background[5, 5] = new SurfaceCell(" ", null, null, Sixel: CreateTestSixel(20, 20));
        composite.AddLayer(background, 0, 0);
        
        // Large opaque overlay covering it completely
        var overlay = new Surface(15, 10, metrics);
        overlay.Fill(new Hex1b.Layout.Rect(0, 0, 15, 10), 
            new SurfaceCell(" ", null, Hex1bColor.Red));
        composite.AddLayer(overlay, 0, 0);
        
        var tokens = SurfaceComparer.SixelFragmentsToTokens(composite);
        
        Assert.IsEmpty(tokens);
    }

    [TestMethod]
    public void T6_7_CompositeToAnsiString_IncludesSixelPayload()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(80, 24, metrics);
        
        var layer = new Surface(40, 12, metrics);
        layer[0, 0] = new SurfaceCell(" ", null, null, Sixel: CreateTestSixel(30, 18));
        composite.AddLayer(layer, 0, 0);
        
        var ansiString = SurfaceComparer.CompositeToAnsiString(composite);
        
        // Should contain sixel DCS sequence
        Assert.Contains("\x1bP", ansiString);
        Assert.Contains("\x1b\\", ansiString); // ST terminator
    }

    [TestMethod]
    public void T6_8_EmptyComposite_NoSixelTokens()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(80, 24, metrics);
        
        // Empty layer
        var layer = new Surface(80, 24, metrics);
        composite.AddLayer(layer, 0, 0);
        
        var tokens = SurfaceComparer.SixelFragmentsToTokens(composite);
        
        Assert.IsEmpty(tokens);
    }

    #endregion

    #region T7: Edge Cases and Multiple Sixel Tests

    [TestMethod]
    public void T7_1_SixelVsSixelOcclusion_UpperLayerWins()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(40, 20, metrics);
        
        // Layer 0: Large sixel at (0, 0)
        var background = new Surface(40, 20, metrics);
        var lowerSixel = CreateTestSixel(100, 60); // 10x3 cells
        background[0, 0] = new SurfaceCell(" ", null, null, Sixel: lowerSixel);
        composite.AddLayer(background, 0, 0);
        
        // Layer 1: Smaller sixel overlapping partially at (3, 0)
        var foreground = new Surface(20, 10, metrics);
        var upperSixel = CreateTestSixel(40, 40); // 4x2 cells
        foreground[0, 0] = new SurfaceCell(" ", null, null, Sixel: upperSixel);
        composite.AddLayer(foreground, 3, 0);
        
        var fragments = composite.GetSixelFragments();
        
        // Should have fragments from both sixels
        // Lower sixel should be fragmented around upper sixel
        // Upper sixel should be complete or minimal fragmentation
        Assert.IsTrue(fragments.Count >= 2);
        
        // Find the upper sixel's fragment(s) - should be at position (3, 0)
        var upperFragments = fragments.Where(f => f.CellPosition.X >= 3).ToList();
        Assert.IsNotEmpty(upperFragments);
    }

    [TestMethod]
    public void T7_2_TwoSixelsSameLayer_NoOcclusion()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(80, 24, metrics);
        
        // Two non-overlapping sixels on same layer
        var layer = new Surface(80, 24, metrics);
        layer[0, 0] = new SurfaceCell(" ", null, null, Sixel: CreateTestSixel(50, 30)); // 5x2 cells
        layer[20, 10] = new SurfaceCell(" ", null, null, Sixel: CreateTestSixel(50, 30));
        composite.AddLayer(layer, 0, 0);
        
        var fragments = composite.GetSixelFragments();
        
        // Both sixels should be complete (no occlusion from same layer)
        Assert.AreEqual(2, fragments.Count);
        Assert.IsTrue(fragments.All(f => f.IsComplete));
    }

    [TestMethod]
    public void T7_3_SixelFullyHiddenByUpperSixel()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(40, 20, metrics);
        
        // Layer 0: Small sixel at (2, 1) which is within the upper sixel's bounds
        var background = new Surface(20, 10, metrics);
        background[2, 1] = new SurfaceCell(" ", null, null, Sixel: CreateTestSixel(20, 20)); // 2x1 cells
        composite.AddLayer(background, 0, 0);
        
        // Layer 1: Large sixel covering it completely (starts at 0,0, spans 10x3 cells)
        var foreground = new Surface(40, 20, metrics);
        foreground[0, 0] = new SurfaceCell(" ", null, null, Sixel: CreateTestSixel(100, 60)); // 10x3 cells
        composite.AddLayer(foreground, 0, 0);
        
        var fragments = composite.GetSixelFragments();
        
        // Only the upper sixel should appear (lower is fully occluded)
        // Lower sixel at (2, 1) + 2x1 cells = covers cells (2-3, 1)
        // Upper sixel at (0, 0) + 10x3 cells = covers cells (0-9, 0-2)
        // So lower is completely inside upper's coverage
        TestSeq.Single(fragments);
        Assert.AreEqual((0, 0), fragments[0].CellPosition);
    }

    [TestMethod]
    public void T7_4_SixelAtNegativeOffset_Clipped()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(40, 20, metrics);
        
        // Sixel anchored at negative position (partially visible)
        var layer = new Surface(50, 30, metrics);
        layer[0, 0] = new SurfaceCell(" ", null, null, Sixel: CreateTestSixel(100, 60));
        composite.AddLayer(layer, -2, -1); // Offset puts anchor at (-2, -1)
        
        var fragments = composite.GetSixelFragments();
        
        // Sixel anchor is outside bounds, but may have visible portions
        // This depends on how we handle anchor-outside-bounds case
        // For now, just verify no crash and fragment positions are valid
        foreach (var fragment in fragments)
        {
            Assert.IsTrue(fragment.CellPosition.X >= 0, "Fragment X should be >= 0");
            Assert.IsTrue(fragment.CellPosition.Y >= 0, "Fragment Y should be >= 0");
        }
    }

    [TestMethod]
    public void T7_5_ManySixels_PerformanceReasonable()
    {
        var metrics = new CellMetrics(10, 20);
        var composite = new CompositeSurface(200, 100, metrics);
        
        // Add many small sixels
        var layer = new Surface(200, 100, metrics);
        var sixelRef = CreateTestSixel(20, 20); // Reuse same sixel
        
        for (var y = 0; y < 10; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                layer[x * 10, y * 10] = new SurfaceCell(" ", null, null, Sixel: sixelRef);
            }
        }
        composite.AddLayer(layer, 0, 0);
        
        // Should complete in reasonable time (not a strict timing test)
        var fragments = composite.GetSixelFragments();
        
        // Each sixel should be present
        Assert.IsTrue(fragments.Count >= 100);
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
