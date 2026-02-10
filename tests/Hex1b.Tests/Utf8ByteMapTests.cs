using Hex1b.Documents;
using System.Text;

namespace Hex1b.Tests;

public class Utf8ByteMapTests
{
    // ═══════════════════════════════════════════════════════════
    // SECTION 1: Construction and TotalBytes
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void EmptyString_ZeroBytes()
    {
        var map = new Utf8ByteMap("");
        Assert.Equal(0, map.TotalBytes);
        Assert.Equal(0, map.CharCount);
    }

    [Fact]
    public void AsciiString_ByteCountEqualsCharCount()
    {
        var map = new Utf8ByteMap("Hello");
        Assert.Equal(5, map.TotalBytes);
        Assert.Equal(5, map.CharCount);
    }

    [Fact]
    public void TwoByte_CharCountDiffersFromByteCount()
    {
        // © = C2 A9 (2 bytes)
        var map = new Utf8ByteMap("©");
        Assert.Equal(2, map.TotalBytes);
        Assert.Equal(1, map.CharCount);
    }

    [Fact]
    public void ThreeByte_BOM()
    {
        // BOM U+FEFF = EF BB BF (3 bytes)
        var map = new Utf8ByteMap("\uFEFF");
        Assert.Equal(3, map.TotalBytes);
        Assert.Equal(1, map.CharCount);
    }

    [Fact]
    public void MixedContent_CorrectTotalBytes()
    {
        // "A©" = 41 C2 A9 = 3 bytes, 2 chars
        var map = new Utf8ByteMap("A©");
        Assert.Equal(3, map.TotalBytes);
        Assert.Equal(2, map.CharCount);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 2: ByteToChar
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void ByteToChar_AsciiString_OneToOneMapping()
    {
        var map = new Utf8ByteMap("ABC");
        Assert.Equal((0, 0), map.ByteToChar(0));
        Assert.Equal((1, 0), map.ByteToChar(1));
        Assert.Equal((2, 0), map.ByteToChar(2));
    }

    [Fact]
    public void ByteToChar_TwoByteChar_BothBytesMappedCorrectly()
    {
        // © = C2 A9
        var map = new Utf8ByteMap("©");
        Assert.Equal((0, 0), map.ByteToChar(0)); // C2 → char 0, byte 0 within char
        Assert.Equal((0, 1), map.ByteToChar(1)); // A9 → char 0, byte 1 within char
    }

    [Fact]
    public void ByteToChar_ThreeByteBOM_AllThreeBytesMapped()
    {
        // BOM = EF BB BF
        var map = new Utf8ByteMap("\uFEFF");
        Assert.Equal((0, 0), map.ByteToChar(0)); // EF → char 0, byte 0
        Assert.Equal((0, 1), map.ByteToChar(1)); // BB → char 0, byte 1
        Assert.Equal((0, 2), map.ByteToChar(2)); // BF → char 0, byte 2
    }

    [Fact]
    public void ByteToChar_MixedContent_CorrectMapping()
    {
        // "A©B" = 41 C2 A9 42
        var map = new Utf8ByteMap("A©B");

        Assert.Equal((0, 0), map.ByteToChar(0)); // 41 → 'A'
        Assert.Equal((1, 0), map.ByteToChar(1)); // C2 → '©', byte 0
        Assert.Equal((1, 1), map.ByteToChar(2)); // A9 → '©', byte 1
        Assert.Equal((2, 0), map.ByteToChar(3)); // 42 → 'B'
    }

    [Fact]
    public void ByteToChar_BOMThenAscii_CrossesBoundary()
    {
        // "\uFEFFHi" = EF BB BF 48 69
        var map = new Utf8ByteMap("\uFEFFHi");
        Assert.Equal((0, 0), map.ByteToChar(0)); // EF
        Assert.Equal((0, 1), map.ByteToChar(1)); // BB
        Assert.Equal((0, 2), map.ByteToChar(2)); // BF
        Assert.Equal((1, 0), map.ByteToChar(3)); // 48 → 'H'
        Assert.Equal((2, 0), map.ByteToChar(4)); // 69 → 'i'
    }

    [Fact]
    public void ByteToChar_OutOfRange_Throws()
    {
        var map = new Utf8ByteMap("A");
        Assert.Throws<ArgumentOutOfRangeException>(() => map.ByteToChar(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.ByteToChar(1));
    }

    [Fact]
    public void ByteToChar_EmptyString_Throws()
    {
        var map = new Utf8ByteMap("");
        Assert.Throws<ArgumentOutOfRangeException>(() => map.ByteToChar(0));
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 3: CharToByteStart / CharByteLength
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void CharToByteStart_AsciiString_Sequential()
    {
        var map = new Utf8ByteMap("ABC");
        Assert.Equal(0, map.CharToByteStart(0));
        Assert.Equal(1, map.CharToByteStart(1));
        Assert.Equal(2, map.CharToByteStart(2));
    }

    [Fact]
    public void CharToByteStart_MixedContent_SkipsMultibyte()
    {
        // "A©B" = 41 C2 A9 42
        var map = new Utf8ByteMap("A©B");
        Assert.Equal(0, map.CharToByteStart(0)); // 'A' at byte 0
        Assert.Equal(1, map.CharToByteStart(1)); // '©' at byte 1
        Assert.Equal(3, map.CharToByteStart(2)); // 'B' at byte 3
    }

    [Fact]
    public void CharByteLength_AsciiChars_AllOne()
    {
        var map = new Utf8ByteMap("Hi");
        Assert.Equal(1, map.CharByteLength(0));
        Assert.Equal(1, map.CharByteLength(1));
    }

    [Fact]
    public void CharByteLength_TwoByteChar_ReturnsTwo()
    {
        var map = new Utf8ByteMap("©");
        Assert.Equal(2, map.CharByteLength(0));
    }

    [Fact]
    public void CharByteLength_ThreeByteChar_ReturnsThree()
    {
        var map = new Utf8ByteMap("\uFEFF");
        Assert.Equal(3, map.CharByteLength(0));
    }

    [Fact]
    public void CharToByteStart_OutOfRange_Throws()
    {
        var map = new Utf8ByteMap("A");
        Assert.Throws<ArgumentOutOfRangeException>(() => map.CharToByteStart(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.CharToByteStart(1));
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 4: Byte slice extraction via CharToByteStart + CharByteLength
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void CharByteSlice_AsciiChar_ReturnsSingleByte()
    {
        var text = "ABC";
        var map = new Utf8ByteMap(text);
        var allBytes = Encoding.UTF8.GetBytes(text);

        var start = map.CharToByteStart(1);
        var len = map.CharByteLength(1);
        var bytes = allBytes.AsSpan(start, len);
        Assert.Equal(1, bytes.Length);
        Assert.Equal((byte)'B', bytes[0]);
    }

    [Fact]
    public void CharByteSlice_TwoByteChar_ReturnsBothBytes()
    {
        var text = "©";
        var map = new Utf8ByteMap(text);
        var allBytes = Encoding.UTF8.GetBytes(text);

        var start = map.CharToByteStart(0);
        var len = map.CharByteLength(0);
        var bytes = allBytes.AsSpan(start, len);
        Assert.Equal(2, bytes.Length);
        Assert.Equal(0xC2, bytes[0]);
        Assert.Equal(0xA9, bytes[1]);
    }

    [Fact]
    public void CharByteSlice_BOM_ReturnsThreeBytes()
    {
        var text = "\uFEFF";
        var map = new Utf8ByteMap(text);
        var allBytes = Encoding.UTF8.GetBytes(text);

        var start = map.CharToByteStart(0);
        var len = map.CharByteLength(0);
        var bytes = allBytes.AsSpan(start, len);
        Assert.Equal(3, bytes.Length);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 5: Roundtrip consistency
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Hello, World!")]
    [InlineData("A©B")]
    [InlineData("\uFEFFHello")]
    [InlineData("café")]
    [InlineData("")]
    public void Roundtrip_EveryByte_MapsToValidChar(string text)
    {
        var map = new Utf8ByteMap(text);
        var allBytes = Encoding.UTF8.GetBytes(text);
        Assert.Equal(allBytes.Length, map.TotalBytes);

        for (var b = 0; b < map.TotalBytes; b++)
        {
            var (charIdx, byteWithin) = map.ByteToChar(b);
            Assert.True(charIdx >= 0 && charIdx < map.CharCount);
            Assert.True(byteWithin >= 0 && byteWithin < map.CharByteLength(charIdx));
            Assert.Equal(b, map.CharToByteStart(charIdx) + byteWithin);
        }
    }

    [Theory]
    [InlineData("Hello, World!")]
    [InlineData("A©B")]
    [InlineData("\uFEFFHello")]
    [InlineData("café")]
    public void Roundtrip_EveryChar_ByteStartPlusLengthCoversRange(string text)
    {
        var map = new Utf8ByteMap(text);

        for (var c = 0; c < map.CharCount; c++)
        {
            var start = map.CharToByteStart(c);
            var len = map.CharByteLength(c);

            // Every byte in [start, start+len) should map back to char c
            for (var b = start; b < start + len; b++)
            {
                var (charIdx, byteWithin) = map.ByteToChar(b);
                Assert.Equal(c, charIdx);
                Assert.Equal(b - start, byteWithin);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 6: Raw byte constructor — invalid UTF-8
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void RawBytes_SingleInvalidByte_OneCharOneSourceByte()
    {
        // 0xAA is an invalid continuation byte → 1 replacement char consuming 1 source byte
        var map = new Utf8ByteMap(new byte[] { 0xAA });
        Assert.Equal(1, map.TotalBytes);
        Assert.Equal(1, map.CharCount);
        Assert.Equal(0, map.CharToByteStart(0));
        Assert.Equal(1, map.CharByteLength(0));
    }

    [Fact]
    public void RawBytes_InvalidByteThenAscii_EachByteIsOneChar()
    {
        // [0xAA, 0x20, 0x48] → 3 chars, each 1 source byte
        var bytes = new byte[] { 0xAA, 0x20, 0x48 };
        var map = new Utf8ByteMap(bytes);

        Assert.Equal(3, map.TotalBytes);
        Assert.Equal(3, map.CharCount);

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(i, map.CharToByteStart(i));
            Assert.Equal(1, map.CharByteLength(i));
            var (charIdx, byteWithin) = map.ByteToChar(i);
            Assert.Equal(i, charIdx);
            Assert.Equal(0, byteWithin);
        }
    }

    [Fact]
    public void RawBytes_InvalidByteAfterValidMultibyte_CorrectMapping()
    {
        // © = C2 A9 (valid 2-byte), then 0xBB (invalid), then 'X' (41)
        var bytes = new byte[] { 0xC2, 0xA9, 0xBB, 0x41 };
        var map = new Utf8ByteMap(bytes);

        Assert.Equal(4, map.TotalBytes);
        Assert.Equal(3, map.CharCount);

        // Char 0: © at bytes 0-1 (2 source bytes)
        Assert.Equal(0, map.CharToByteStart(0));
        Assert.Equal(2, map.CharByteLength(0));

        // Char 1: replacement at byte 2 (1 source byte)
        Assert.Equal(2, map.CharToByteStart(1));
        Assert.Equal(1, map.CharByteLength(1));

        // Char 2: 'X' at byte 3 (1 source byte)
        Assert.Equal(3, map.CharToByteStart(2));
        Assert.Equal(1, map.CharByteLength(2));
    }

    [Fact]
    public void RawBytes_TruncatedMultibyte_ConsumesAvailableBytes()
    {
        // 0xC2 alone is a truncated 2-byte sequence → 1 replacement char, 1 source byte
        var bytes = new byte[] { 0xC2 };
        var map = new Utf8ByteMap(bytes);

        Assert.Equal(1, map.TotalBytes);
        Assert.Equal(1, map.CharCount);
        Assert.Equal(0, map.CharToByteStart(0));
        Assert.Equal(1, map.CharByteLength(0));
    }

    [Fact]
    public void RawBytes_AllInvalidBytes_EachIsOneChar()
    {
        // All continuation bytes without leaders
        var bytes = new byte[] { 0x80, 0x90, 0xA0, 0xBF, 0xFE, 0xFF };
        var map = new Utf8ByteMap(bytes);

        Assert.Equal(6, map.TotalBytes);
        Assert.Equal(6, map.CharCount);

        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(i, map.CharToByteStart(i));
            Assert.Equal(1, map.CharByteLength(i));
        }
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x41)]
    [InlineData(0x7F)]
    [InlineData(0x80)]
    [InlineData(0xAA)]
    [InlineData(0xBF)]
    [InlineData(0xC0)]
    [InlineData(0xFE)]
    [InlineData(0xFF)]
    public void RawBytes_AnySingleByte_ProducesExactlyOneChar(byte b)
    {
        var map = new Utf8ByteMap(new byte[] { b });
        Assert.Equal(1, map.TotalBytes);
        Assert.Equal(1, map.CharCount);
        Assert.Equal(0, map.CharToByteStart(0));
        Assert.Equal(1, map.CharByteLength(0));
    }

    [Fact]
    public void RawBytes_ByteToChar_EveryByteNavigable()
    {
        // Mixed valid and invalid: [0xAA, 0xC2, 0xA9, 0xBB, 0x41]
        // Expected: char 0 = byte 0 (invalid), char 1 = bytes 1-2 (©), char 2 = byte 3 (invalid), char 3 = byte 4 ('A')
        var bytes = new byte[] { 0xAA, 0xC2, 0xA9, 0xBB, 0x41 };
        var map = new Utf8ByteMap(bytes);

        Assert.Equal(5, map.TotalBytes);
        Assert.Equal(4, map.CharCount);

        // Every single byte maps to a char and can be reached
        for (int b = 0; b < map.TotalBytes; b++)
        {
            var (charIdx, _) = map.ByteToChar(b);
            Assert.True(charIdx >= 0 && charIdx < map.CharCount,
                $"Byte {b} (0x{bytes[b]:X2}) should map to a valid char index, got {charIdx}");
        }
    }
}
