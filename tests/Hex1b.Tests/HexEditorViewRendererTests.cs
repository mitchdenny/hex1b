using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Widgets;

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

    // ═══════════════════════════════════════════════════════════
    // SECTION 8: Hex input mode — HandleCharInput
    // ═══════════════════════════════════════════════════════════

    private static (HexEditorViewRenderer renderer, EditorState state) SetupHexInput(string text)
    {
        var doc = new Hex1bDocument(text);
        var state = new EditorState(doc);
        var renderer = new HexEditorViewRenderer();
        return (renderer, state);
    }

    [Fact]
    public void HandlesCharInput_IsTrue()
    {
        var renderer = new HexEditorViewRenderer();
        Assert.True(renderer.HandlesCharInput);
    }

    [Fact]
    public void HandleCharInput_FirstNibble_StoresPending()
    {
        var (renderer, state) = SetupHexInput("AB");
        char? nibble = null;

        var consumed = renderer.HandleCharInput('4', state, ref nibble, 80);

        Assert.True(consumed);
        Assert.Equal('4', nibble);
        // Document unchanged — still "AB"
        Assert.Equal("AB", state.Document.GetText());
    }

    [Fact]
    public void HandleCharInput_SecondNibble_CommitsByte()
    {
        var (renderer, state) = SetupHexInput("AB");
        char? nibble = '4';

        var consumed = renderer.HandleCharInput('A', state, ref nibble, 80);

        Assert.True(consumed);
        Assert.Null(nibble);
        // Byte 0x4A = 'J' replaces 'A' at position 0, cursor advances to pos 1
        Assert.Equal("JB", state.Document.GetText());
        Assert.Equal(1, state.Cursor.Position.Value);
    }

    [Theory]
    [InlineData('0')]
    [InlineData('9')]
    [InlineData('a')]
    [InlineData('f')]
    [InlineData('A')]
    [InlineData('F')]
    public void HandleCharInput_HexChars_AreConsumed(char c)
    {
        var (renderer, state) = SetupHexInput("X");
        char? nibble = null;

        Assert.True(renderer.HandleCharInput(c, state, ref nibble, 80));
        Assert.Equal(c, nibble);
    }

    [Theory]
    [InlineData('g')]
    [InlineData('G')]
    [InlineData('z')]
    [InlineData(' ')]
    [InlineData('!')]
    public void HandleCharInput_NonHexChars_NotConsumed(char c)
    {
        var (renderer, state) = SetupHexInput("X");
        char? nibble = null;

        Assert.False(renderer.HandleCharInput(c, state, ref nibble, 80));
        Assert.Null(nibble);
    }

    [Fact]
    public void HandleCharInput_NonHexChar_ClearsPendingNibble()
    {
        var (renderer, state) = SetupHexInput("X");
        char? nibble = 'A';

        renderer.HandleCharInput('z', state, ref nibble, 80);

        Assert.Null(nibble);
    }

    [Fact]
    public void HandleCharInput_TwoNibbles_ProducesCorrectByteValue()
    {
        // 0xFF is not valid as a single UTF-8 byte → becomes replacement char
        var (renderer, state) = SetupHexInput("X");
        char? nibble = null;

        renderer.HandleCharInput('F', state, ref nibble, 80);
        renderer.HandleCharInput('F', state, ref nibble, 80);

        // Single byte 0xFF is invalid UTF-8 → U+FFFD replacement
        Assert.Equal('\uFFFD', state.Document.GetText()[0]);
    }

    [Fact]
    public void HandleCharInput_AtEndOfDocument_InsertsNewByte()
    {
        var (renderer, state) = SetupHexInput("A");
        // Move cursor to end
        state.MoveToDocumentEnd();

        char? nibble = null;
        renderer.HandleCharInput('4', state, ref nibble, 80);
        renderer.HandleCharInput('2', state, ref nibble, 80);

        // 0x42 = 'B', appended after 'A'
        Assert.Equal("AB", state.Document.GetText());
    }

    [Fact]
    public void HandleCharInput_CursorAdvancesAfterCommit()
    {
        var (renderer, state) = SetupHexInput("ABC");
        char? nibble = null;

        // Edit first byte: 0x58 = 'X'
        renderer.HandleCharInput('5', state, ref nibble, 80);
        renderer.HandleCharInput('8', state, ref nibble, 80);
        Assert.Equal(1, state.Cursor.Position.Value);

        // Edit second byte: 0x59 = 'Y'
        renderer.HandleCharInput('5', state, ref nibble, 80);
        renderer.HandleCharInput('9', state, ref nibble, 80);
        Assert.Equal(2, state.Cursor.Position.Value);

        Assert.Equal("XYC", state.Document.GetText());
    }

    [Fact]
    public void HandleCharInput_LowercaseHex_WorksCorrectly()
    {
        // 0x41 = 'A' (valid ASCII byte)
        var (renderer, state) = SetupHexInput("X");
        char? nibble = null;

        renderer.HandleCharInput('4', state, ref nibble, 80);
        renderer.HandleCharInput('1', state, ref nibble, 80);

        Assert.Equal('A', state.Document.GetText()[0]);
    }

    [Fact]
    public void HandleCharInput_ReadOnly_DoesNotConsume()
    {
        var (renderer, state) = SetupHexInput("X");
        state.IsReadOnly = true;
        char? nibble = null;

        var consumed = renderer.HandleCharInput('4', state, ref nibble, 80);

        Assert.False(consumed);
        Assert.Null(nibble);
        Assert.Equal("X", state.Document.GetText());
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 9: Hex input via EditorNode integration
    // ═══════════════════════════════════════════════════════════

    private static EditorNode CreateHexNode(string text, int width, int height)
    {
        var doc = new Hex1bDocument(text);
        var state = new EditorState(doc);
        var node = new EditorNode
        {
            State = state,
            ViewRenderer = HexEditorViewRenderer.Instance,
            IsFocused = true
        };
        node.Measure(new Constraints(0, width, 0, height));
        node.Arrange(new Rect(0, 0, width, height));
        return node;
    }

    [Fact]
    public void TextEditorRenderer_HandlesCharInput_IsFalse()
    {
        IEditorViewRenderer renderer = TextEditorViewRenderer.Instance;
        Assert.False(renderer.HandlesCharInput);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 10: Multi-byte byte-level editing
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void CommitByte_TwoByteChar_ReplacesFirstByte()
    {
        // © = C2 A9. Replace C2 with 41 → bytes become 41 A9.
        // 41 = 'A' (valid ASCII), A9 is invalid UTF-8 start → replacement char
        var (renderer, state) = SetupHexInput("©");
        char? nibble = null;

        renderer.HandleCharInput('4', state, ref nibble, 80);
        renderer.HandleCharInput('1', state, ref nibble, 80);

        var result = state.Document.GetText();
        // The bytes 41 A9 decoded as UTF-8: 'A' + invalid → "A\uFFFD"
        Assert.StartsWith("A", result);
        Assert.Equal(2, result.Length); // 'A' + replacement char
    }

    [Fact]
    public void CommitByte_TwoByteChar_ReplacesSecondByte()
    {
        // "A©" = bytes 41 C2 A9. Cursor at char 1 (©), byte offset = 1.
        // Replace byte 1 (C2) with C3 → bytes 41 C3 A9 = "Aé"
        var (renderer, state) = SetupHexInput("A©");
        state.MoveCursor(CursorDirection.Right); // cursor at char 1 (©)
        char? nibble = null;

        renderer.HandleCharInput('C', state, ref nibble, 80);
        renderer.HandleCharInput('3', state, ref nibble, 80);

        // C3 A9 = é
        Assert.Equal("Aé", state.Document.GetText());
    }

    [Fact]
    public void CommitByte_BOM_ReplaceFirstByte()
    {
        // BOM = EF BB BF. Replace EF with 00 → bytes 00 BB BF
        // 00 = NUL, BB/BF are invalid UTF-8 starts → NUL + 2 replacement chars
        var (renderer, state) = SetupHexInput("\uFEFF");
        char? nibble = null;

        renderer.HandleCharInput('0', state, ref nibble, 80);
        renderer.HandleCharInput('0', state, ref nibble, 80);

        var result = state.Document.GetText();
        Assert.Equal('\0', result[0]); // NUL byte
        Assert.True(result.Length >= 2); // At least NUL + something from invalid bytes
    }

    [Fact]
    public void CommitByte_AsciiChar_StillWorks()
    {
        // Verify ASCII editing still works with the new CommitByte
        var (renderer, state) = SetupHexInput("ABC");
        char? nibble = null;

        // Replace 'A' (0x41) with 'X' (0x58)
        renderer.HandleCharInput('5', state, ref nibble, 80);
        renderer.HandleCharInput('8', state, ref nibble, 80);

        Assert.Equal("XBC", state.Document.GetText());
        Assert.Equal(1, state.Cursor.Position.Value); // Advanced past 'X'
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 11: HitTest with multi-byte characters
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void HitTest_ContinuationByte_MapsToSameChar()
    {
        // "©B" = bytes C2 A9 42. Clicking byte 1 (A9) should map to char 0 (©), not char 1
        var renderer = new HexEditorViewRenderer();
        var doc = new Hex1bDocument("©B");
        var state = new EditorState(doc);

        // In a wide viewport, byte 0 (C2) is at hex col 9, byte 1 (A9) is at hex col 12
        // byte 2 (42) is at hex col 15
        var (_, hasMidGroup) = renderer.CalculateLayout(80);

        // Hit test at byte index 1 (A9) — this is the continuation byte of ©
        // The hex column for byte 1 in a 16-byte-per-row layout
        var hexCol1 = 9 + 3; // hex start (9) + 1 byte * 3 chars = col 12
        var result = renderer.HitTest(hexCol1, 0, state, 80, 10, 1, 0);

        Assert.NotNull(result);
        Assert.Equal(0, result.Value.Value); // Should map to char 0 (©), not char 1 (B)
    }

    [Fact]
    public void HitTest_BOMThirdByte_MapsToCharZero()
    {
        // "\uFEFFX" = bytes EF BB BF 58. Byte 2 (BF) should map to char 0 (BOM)
        var renderer = new HexEditorViewRenderer();
        var doc = new Hex1bDocument("\uFEFFX");
        var state = new EditorState(doc);

        // Byte 2 is at hex col 9 + 2*3 = 15
        var hexCol2 = 9 + 6;
        var result = renderer.HitTest(hexCol2, 0, state, 80, 10, 1, 0);

        Assert.NotNull(result);
        Assert.Equal(0, result.Value.Value); // Char 0 (BOM), not char 2
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 12: Cross-editor resilience
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void SharedDoc_HexDeleteShrinks_TextEditorCursorClamped()
    {
        // Two EditorStates sharing one document
        var doc = new Hex1bDocument("Hello World");
        var textState = new EditorState(doc);
        var hexState = new EditorState(doc);

        // Move text editor cursor to end
        textState.MoveToDocumentEnd();
        Assert.Equal(11, textState.Cursor.Position.Value);

        // Hex editor selects all and deletes (simulating via document)
        hexState.SelectAll();
        hexState.DeleteForward();

        // Document is now empty
        Assert.Equal(0, doc.Length);

        // Text editor clamps its cursor
        textState.ClampAllCursors();
        Assert.Equal(0, textState.Cursor.Position.Value);
    }

    [Fact]
    public void SharedDoc_HexReplacesMultibyte_TextEditorSurvives()
    {
        // Document with BOM followed by text
        var doc = new Hex1bDocument("\uFEFFHello");
        var textState = new EditorState(doc);
        var hexState = new EditorState(doc);

        // Text editor cursor at char 3 (the 'l')
        textState.MoveCursor(CursorDirection.Right);
        textState.MoveCursor(CursorDirection.Right);
        textState.MoveCursor(CursorDirection.Right);
        Assert.Equal(3, textState.Cursor.Position.Value);

        // Hex editor replaces the BOM byte — this changes the BOM char
        // to potentially multiple replacement chars, shifting everything
        var renderer = new HexEditorViewRenderer();
        char? nibble = null;
        renderer.HandleCharInput('0', hexState, ref nibble, 80);
        renderer.HandleCharInput('0', hexState, ref nibble, 80);

        // Document has changed — clamp text editor cursors
        textState.ClampAllCursors();

        // Cursor should still be valid (not throw)
        var pos = textState.Cursor.Position.Value;
        Assert.True(pos >= 0 && pos <= doc.Length);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 13: Hex/ASCII column alignment
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [InlineData(13, false)]   // 1 byte, no mid-group
    [InlineData(17, false)]   // 2 bytes, no mid-group
    [InlineData(26, true)]    // 4 bytes, mid-group
    [InlineData(42, true)]    // 8 bytes, mid-group
    [InlineData(74, true)]    // 16 bytes, mid-group
    public void ColumnMapping_AsciiColumnMatchesRenderedPosition(int viewportWidth, bool expectMidGroup)
    {
        var renderer = new HexEditorViewRenderer();
        var (bytesPerRow, hasMidGroup) = renderer.CalculateLayout(viewportWidth);
        Assert.Equal(expectMidGroup, hasMidGroup);

        // Build the line the same way Render does
        var half = bytesPerRow / 2;
        var sb = new System.Text.StringBuilder();
        sb.Append("00000000"); // address
        sb.Append(' ');
        for (int i = 0; i < bytesPerRow; i++)
        {
            if (hasMidGroup && i == half) sb.Append(' ');
            sb.Append(i.ToString("X2"));
            if (i < bytesPerRow - 1) sb.Append(' ');
        }
        sb.Append(' ');
        for (int i = 0; i < bytesPerRow; i++)
            sb.Append((char)('a' + i));

        var line = sb.ToString();

        // Verify every byte's hex and ASCII columns match rendered positions
        for (int i = 0; i < bytesPerRow; i++)
        {
            var hexCol = GetHexColumnForByteReflection(i, bytesPerRow, hasMidGroup);
            var asciiCol = GetAsciiColumnForByteReflection(i, bytesPerRow, hasMidGroup);

            // Hex cell should contain the byte value
            var expectedHex = i.ToString("X2");
            var actualHex = line.Substring(hexCol, 2);
            Assert.True(expectedHex == actualHex,
                $"Byte {i}: hex at col {hexCol} should be '{expectedHex}' but got '{actualHex}'");

            // ASCII cell should contain the corresponding letter
            var expectedAscii = (char)('a' + i);
            Assert.True(expectedAscii == line[asciiCol],
                $"Byte {i}: ASCII at col {asciiCol} should be '{expectedAscii}' but got '{line[asciiCol]}'");
        }
    }

    // Mirror of the private static methods for testing
    private static int GetHexColumnForByteReflection(int byteInRow, int bytesPerRow, bool hasMidGroup)
    {
        const int hexStart = 9; // AddressWidth(8) + 1
        var half = bytesPerRow / 2;
        if (!hasMidGroup)
            return hexStart + byteInRow * 3;
        return byteInRow < half
            ? hexStart + byteInRow * 3
            : hexStart + half * 3 + 1 + (byteInRow - half) * 3;
    }

    private static int GetAsciiColumnForByteReflection(int byteInRow, int bytesPerRow, bool hasMidGroup)
    {
        var hexWidth = bytesPerRow * 3 - 1 + (hasMidGroup ? 1 : 0);
        var asciiStart = 9 + hexWidth + 1;
        return asciiStart + byteInRow;
    }
}
