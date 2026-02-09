using Hex1b.Documents;

namespace Hex1b.Tests;

public class HexEditorViewRendererTests
{
    // ═══════════════════════════════════════════════════════════
    // SECTION 1: Layout calculation — fluid mode (no snap points)
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [InlineData(13, 1, false)]   // Absolute minimum: "XXXXXXXX XX ."
    [InlineData(14, 1, false)]   // 1 byte still (4*2+9=17 needed for 2)
    [InlineData(17, 2, false)]   // 4*2+9=17 → 2 bytes
    [InlineData(20, 2, false)]   // 4*3+9=21 needed for 3
    [InlineData(21, 3, false)]   // 4*3+9=21 → 3 bytes
    [InlineData(25, 4, false)]   // 4*4+9=25 fits exactly; 4*4+10=26 doesn't → no mid-group
    [InlineData(26, 4, true)]    // 4*4+10=26 → 4 bytes with mid-group
    [InlineData(42, 8, true)]    // 4*8+10=42 → 8 bytes with mid-group
    [InlineData(73, 16, false)]  // 4*16+9=73 fits exactly; 4*16+10=74 doesn't → no mid-group
    [InlineData(74, 16, true)]   // 4*16+10=74 → 16 bytes with mid-group
    [InlineData(200, 16, true)]  // Capped at MaxBytesPerRow=16
    public void CalculateLayout_Fluid_ReturnsExpectedBytesPerRow(int width, int expectedBytes, bool expectedMidGroup)
    {
        var renderer = new HexEditorViewRenderer(); // fluid (no snap points)
        var (bytesPerRow, hasMidGroup) = renderer.CalculateLayout(width);

        Assert.Equal(expectedBytes, bytesPerRow);
        Assert.Equal(expectedMidGroup, hasMidGroup);
    }

    [Theory]
    [InlineData(10)]  // Below absolute minimum
    [InlineData(12)]
    public void CalculateLayout_BelowMinimum_Returns1Byte(int width)
    {
        var renderer = new HexEditorViewRenderer();
        var (bytesPerRow, _) = renderer.CalculateLayout(width);
        Assert.Equal(1, bytesPerRow);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 2: Layout calculation — snap points
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [InlineData(13, 1)]   // Only 1 fits → snap to 1
    [InlineData(41, 8)]   // (41-9)/4=8 → snap to 8
    [InlineData(42, 8)]   // 4*8+10=42 → 8 bytes with mid-group, snap to 8
    [InlineData(73, 16)]  // (73-9)/4=16 → snap to 16
    [InlineData(74, 16)]  // 4*16+10=74 → snap to 16
    public void CalculateLayout_StandardSnaps_SnapsCorrectly(int width, int expectedBytes)
    {
        var renderer = new HexEditorViewRenderer { SnapPoints = HexEditorViewRenderer.StandardSnaps };
        var (bytesPerRow, _) = renderer.CalculateLayout(width);
        Assert.Equal(expectedBytes, bytesPerRow);
    }

    [Theory]
    [InlineData(13, 1)]
    [InlineData(17, 2)]
    [InlineData(21, 2)]   // 3 bytes fit but snap down to 2
    [InlineData(26, 4)]
    [InlineData(41, 8)]   // (41-9)/4=8 → snap to 8
    [InlineData(42, 8)]
    [InlineData(74, 16)]
    public void CalculateLayout_PowerOfTwoSnaps_SnapsCorrectly(int width, int expectedBytes)
    {
        var renderer = new HexEditorViewRenderer { SnapPoints = HexEditorViewRenderer.PowerOfTwoSnaps };
        var (bytesPerRow, _) = renderer.CalculateLayout(width);
        Assert.Equal(expectedBytes, bytesPerRow);
    }

    [Fact]
    public void CalculateLayout_CustomSnaps_RoundsToNearest()
    {
        var renderer = new HexEditorViewRenderer { SnapPoints = [1, 6, 12], MaxBytesPerRow = 12 };
        // width 33 → (33-9)/4=6 → snap to 6
        var (bytes6, _) = renderer.CalculateLayout(33);
        Assert.Equal(6, bytes6);

        // width 40 → (40-9)/4=7 → snap to 6 (7 > 6, 7 < 12)
        var (bytes6b, _) = renderer.CalculateLayout(40);
        Assert.Equal(6, bytes6b);

        // width 58 → (58-9)/4=12 → snap to 12; RowWidth(12,true)=8+1+(12*3-1+1)+1+12=59>58→no gap
        var (bytes12, _) = renderer.CalculateLayout(57);
        Assert.Equal(12, bytes12);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 3: Min/Max constraints
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void CalculateLayout_MinBytesPerRow_NeverGoesBelowMin()
    {
        var renderer = new HexEditorViewRenderer { MinBytesPerRow = 4 };
        // Even at narrow widths where only 2 fit, clamp to 4
        var (bytesPerRow, _) = renderer.CalculateLayout(17); // 2 bytes would fit
        Assert.Equal(4, bytesPerRow);
    }

    [Fact]
    public void CalculateLayout_MaxBytesPerRow_CapsAtMax()
    {
        var renderer = new HexEditorViewRenderer { MaxBytesPerRow = 8 };
        var (bytesPerRow, _) = renderer.CalculateLayout(200); // 16+ would fit
        Assert.Equal(8, bytesPerRow);
    }

    [Fact]
    public void CalculateLayout_SnapPointsWithMinMax_RespectsAll()
    {
        var renderer = new HexEditorViewRenderer
        {
            MinBytesPerRow = 2,
            MaxBytesPerRow = 8,
            SnapPoints = [1, 4, 8, 16]
        };
        // Very narrow: snap to 1, but min is 2 → 2
        var (narrow, _) = renderer.CalculateLayout(13);
        Assert.Equal(2, narrow);

        // Wide: would snap to 16, but max is 8 → 8
        var (wide, _) = renderer.CalculateLayout(200);
        Assert.Equal(8, wide);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 4: GetTotalLines depends on viewport width
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void GetTotalLines_WideViewport_FewerRows()
    {
        // 32 bytes of content
        var doc = new Hex1bDocument("0123456789ABCDEF0123456789ABCDEF");
        var renderer = new HexEditorViewRenderer();

        // At width 74 → 16 bytes/row → ceil(32/16)=2 rows
        var wideLines = renderer.GetTotalLines(doc, 74);
        Assert.Equal(2, wideLines);

        // At width 42 → 8 bytes/row → ceil(32/8)=4 rows
        var narrowLines = renderer.GetTotalLines(doc, 42);
        Assert.Equal(4, narrowLines);

        // At width 13 → 1 byte/row → 32 rows
        var tinyLines = renderer.GetTotalLines(doc, 13);
        Assert.Equal(32, tinyLines);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 5: GetMaxLineWidth never exceeds viewport (no h-scroll)
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [InlineData(13)]
    [InlineData(30)]
    [InlineData(50)]
    [InlineData(80)]
    [InlineData(120)]
    public void GetMaxLineWidth_NeverExceedsViewportWidth(int viewportWidth)
    {
        var doc = new Hex1bDocument("Hello, World! This is a test of the hex editor.");
        var renderer = new HexEditorViewRenderer();
        var maxWidth = renderer.GetMaxLineWidth(doc, 1, 10, viewportWidth);
        Assert.True(maxWidth <= viewportWidth,
            $"Max line width {maxWidth} should not exceed viewport width {viewportWidth}");
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 6: Row format verification
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void CalculateLayout_MinimumWidth_ProducesExpectedFormat()
    {
        // "XXXXXXXX XX ." = 13 chars for 1 byte
        var renderer = new HexEditorViewRenderer();
        var (bytesPerRow, hasMidGroup) = renderer.CalculateLayout(13);
        Assert.Equal(1, bytesPerRow);
        Assert.False(hasMidGroup);
    }

    [Fact]
    public void CalculateLayout_MidGroupAppears_AtFourOrMoreBytes()
    {
        var renderer = new HexEditorViewRenderer();

        // 3 bytes: no mid-group
        var (bytes3, midGroup3) = renderer.CalculateLayout(21);
        Assert.Equal(3, bytes3);
        Assert.False(midGroup3);

        // 4 bytes with gap: width = 4*4+10 = 26
        var (bytes4, midGroup4) = renderer.CalculateLayout(26);
        Assert.Equal(4, bytes4);
        Assert.True(midGroup4);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 7: Fluid mode fills continuously
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void CalculateLayout_Fluid_BytesIncreaseGradually()
    {
        var renderer = new HexEditorViewRenderer();
        int prev = 0;
        for (int w = 13; w <= 80; w++)
        {
            var (bytes, _) = renderer.CalculateLayout(w);
            Assert.True(bytes >= prev, $"Bytes should not decrease: was {prev} at width {w - 1}, got {bytes} at width {w}");
            prev = bytes;
        }
    }

    [Fact]
    public void CalculateLayout_SnapPoints_BytesNeverBetweenSnaps()
    {
        var snaps = new[] { 1, 4, 8, 16 };
        var renderer = new HexEditorViewRenderer { SnapPoints = snaps };
        for (int w = 13; w <= 120; w++)
        {
            var (bytes, _) = renderer.CalculateLayout(w);
            Assert.Contains(bytes, snaps);
        }
    }
}
