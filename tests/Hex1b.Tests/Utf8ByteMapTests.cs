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
    // SECTION 4: GetCharBytes
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void GetCharBytes_AsciiChar_ReturnsSingleByte()
    {
        var text = "ABC";
        var map = new Utf8ByteMap(text);
        var allBytes = Encoding.UTF8.GetBytes(text);

        var bytes = map.GetCharBytes(1, allBytes); // 'B'
        Assert.Equal(1, bytes.Length);
        Assert.Equal((byte)'B', bytes[0]);
    }

    [Fact]
    public void GetCharBytes_TwoByteChar_ReturnsBothBytes()
    {
        var text = "©";
        var map = new Utf8ByteMap(text);
        var allBytes = Encoding.UTF8.GetBytes(text);

        var bytes = map.GetCharBytes(0, allBytes);
        Assert.Equal(2, bytes.Length);
        Assert.Equal(0xC2, bytes[0]);
        Assert.Equal(0xA9, bytes[1]);
    }

    [Fact]
    public void GetCharBytes_BOM_ReturnsThreeBytes()
    {
        var text = "\uFEFF";
        var map = new Utf8ByteMap(text);
        var allBytes = Encoding.UTF8.GetBytes(text);

        var bytes = map.GetCharBytes(0, allBytes);
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
}
