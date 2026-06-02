using Hex1b.Documents;
using System.Text;

namespace Hex1b.Tests;

[TestClass]
public class Utf8ByteMapTests
{
    // ═══════════════════════════════════════════════════════════
    // SECTION 1: Construction and TotalBytes
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public void EmptyString_ZeroBytes()
    {
        var map = new Utf8ByteMap("");
        Assert.AreEqual(0, map.TotalBytes);
        Assert.AreEqual(0, map.CharCount);
    }

    [TestMethod]
    public void AsciiString_ByteCountEqualsCharCount()
    {
        var map = new Utf8ByteMap("Hello");
        Assert.AreEqual(5, map.TotalBytes);
        Assert.AreEqual(5, map.CharCount);
    }

    [TestMethod]
    public void TwoByte_CharCountDiffersFromByteCount()
    {
        // © = C2 A9 (2 bytes)
        var map = new Utf8ByteMap("©");
        Assert.AreEqual(2, map.TotalBytes);
        Assert.AreEqual(1, map.CharCount);
    }

    [TestMethod]
    public void ThreeByte_BOM()
    {
        // BOM U+FEFF = EF BB BF (3 bytes)
        var map = new Utf8ByteMap("\uFEFF");
        Assert.AreEqual(3, map.TotalBytes);
        Assert.AreEqual(1, map.CharCount);
    }

    [TestMethod]
    public void MixedContent_CorrectTotalBytes()
    {
        // "A©" = 41 C2 A9 = 3 bytes, 2 chars
        var map = new Utf8ByteMap("A©");
        Assert.AreEqual(3, map.TotalBytes);
        Assert.AreEqual(2, map.CharCount);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 2: ByteToChar
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public void ByteToChar_AsciiString_OneToOneMapping()
    {
        var map = new Utf8ByteMap("ABC");
        Assert.AreEqual((0, 0), map.ByteToChar(0));
        Assert.AreEqual((1, 0), map.ByteToChar(1));
        Assert.AreEqual((2, 0), map.ByteToChar(2));
    }

    [TestMethod]
    public void ByteToChar_TwoByteChar_BothBytesMappedCorrectly()
    {
        // © = C2 A9
        var map = new Utf8ByteMap("©");
        Assert.AreEqual((0, 0), map.ByteToChar(0)); // C2 → char 0, byte 0 within char
        Assert.AreEqual((0, 1), map.ByteToChar(1)); // A9 → char 0, byte 1 within char
    }

    [TestMethod]
    public void ByteToChar_ThreeByteBOM_AllThreeBytesMapped()
    {
        // BOM = EF BB BF
        var map = new Utf8ByteMap("\uFEFF");
        Assert.AreEqual((0, 0), map.ByteToChar(0)); // EF → char 0, byte 0
        Assert.AreEqual((0, 1), map.ByteToChar(1)); // BB → char 0, byte 1
        Assert.AreEqual((0, 2), map.ByteToChar(2)); // BF → char 0, byte 2
    }

    [TestMethod]
    public void ByteToChar_MixedContent_CorrectMapping()
    {
        // "A©B" = 41 C2 A9 42
        var map = new Utf8ByteMap("A©B");

        Assert.AreEqual((0, 0), map.ByteToChar(0)); // 41 → 'A'
        Assert.AreEqual((1, 0), map.ByteToChar(1)); // C2 → '©', byte 0
        Assert.AreEqual((1, 1), map.ByteToChar(2)); // A9 → '©', byte 1
        Assert.AreEqual((2, 0), map.ByteToChar(3)); // 42 → 'B'
    }

    [TestMethod]
    public void ByteToChar_BOMThenAscii_CrossesBoundary()
    {
        // "\uFEFFHi" = EF BB BF 48 69
        var map = new Utf8ByteMap("\uFEFFHi");
        Assert.AreEqual((0, 0), map.ByteToChar(0)); // EF
        Assert.AreEqual((0, 1), map.ByteToChar(1)); // BB
        Assert.AreEqual((0, 2), map.ByteToChar(2)); // BF
        Assert.AreEqual((1, 0), map.ByteToChar(3)); // 48 → 'H'
        Assert.AreEqual((2, 0), map.ByteToChar(4)); // 69 → 'i'
    }

    [TestMethod]
    public void ByteToChar_OutOfRange_Throws()
    {
        var map = new Utf8ByteMap("A");
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => map.ByteToChar(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => map.ByteToChar(1));
    }

    [TestMethod]
    public void ByteToChar_EmptyString_Throws()
    {
        var map = new Utf8ByteMap("");
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => map.ByteToChar(0));
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 3: CharToByteStart / CharByteLength
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public void CharToByteStart_AsciiString_Sequential()
    {
        var map = new Utf8ByteMap("ABC");
        Assert.AreEqual(0, map.CharToByteStart(0));
        Assert.AreEqual(1, map.CharToByteStart(1));
        Assert.AreEqual(2, map.CharToByteStart(2));
    }

    [TestMethod]
    public void CharToByteStart_MixedContent_SkipsMultibyte()
    {
        // "A©B" = 41 C2 A9 42
        var map = new Utf8ByteMap("A©B");
        Assert.AreEqual(0, map.CharToByteStart(0)); // 'A' at byte 0
        Assert.AreEqual(1, map.CharToByteStart(1)); // '©' at byte 1
        Assert.AreEqual(3, map.CharToByteStart(2)); // 'B' at byte 3
    }

    [TestMethod]
    public void CharByteLength_AsciiChars_AllOne()
    {
        var map = new Utf8ByteMap("Hi");
        Assert.AreEqual(1, map.CharByteLength(0));
        Assert.AreEqual(1, map.CharByteLength(1));
    }

    [TestMethod]
    public void CharByteLength_TwoByteChar_ReturnsTwo()
    {
        var map = new Utf8ByteMap("©");
        Assert.AreEqual(2, map.CharByteLength(0));
    }

    [TestMethod]
    public void CharByteLength_ThreeByteChar_ReturnsThree()
    {
        var map = new Utf8ByteMap("\uFEFF");
        Assert.AreEqual(3, map.CharByteLength(0));
    }

    [TestMethod]
    public void CharToByteStart_OutOfRange_Throws()
    {
        var map = new Utf8ByteMap("A");
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => map.CharToByteStart(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => map.CharToByteStart(1));
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 4: Byte slice extraction via CharToByteStart + CharByteLength
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public void CharByteSlice_AsciiChar_ReturnsSingleByte()
    {
        var text = "ABC";
        var map = new Utf8ByteMap(text);
        var allBytes = Encoding.UTF8.GetBytes(text);

        var start = map.CharToByteStart(1);
        var len = map.CharByteLength(1);
        var bytes = allBytes.AsSpan(start, len);
        Assert.AreEqual(1, bytes.Length);
        Assert.AreEqual((byte)'B', bytes[0]);
    }

    [TestMethod]
    public void CharByteSlice_TwoByteChar_ReturnsBothBytes()
    {
        var text = "©";
        var map = new Utf8ByteMap(text);
        var allBytes = Encoding.UTF8.GetBytes(text);

        var start = map.CharToByteStart(0);
        var len = map.CharByteLength(0);
        var bytes = allBytes.AsSpan(start, len);
        Assert.AreEqual(2, bytes.Length);
        Assert.AreEqual(0xC2, bytes[0]);
        Assert.AreEqual(0xA9, bytes[1]);
    }

    [TestMethod]
    public void CharByteSlice_BOM_ReturnsThreeBytes()
    {
        var text = "\uFEFF";
        var map = new Utf8ByteMap(text);
        var allBytes = Encoding.UTF8.GetBytes(text);

        var start = map.CharToByteStart(0);
        var len = map.CharByteLength(0);
        var bytes = allBytes.AsSpan(start, len);
        Assert.AreEqual(3, bytes.Length);
        Assert.AreEqual(0xEF, bytes[0]);
        Assert.AreEqual(0xBB, bytes[1]);
        Assert.AreEqual(0xBF, bytes[2]);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 5: Roundtrip consistency
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    [DataRow("Hello, World!")]
    [DataRow("A©B")]
    [DataRow("\uFEFFHello")]
    [DataRow("café")]
    [DataRow("")]
    public void Roundtrip_EveryByte_MapsToValidChar(string text)
    {
        var map = new Utf8ByteMap(text);
        var allBytes = Encoding.UTF8.GetBytes(text);
        Assert.AreEqual(allBytes.Length, map.TotalBytes);

        for (var b = 0; b < map.TotalBytes; b++)
        {
            var (charIdx, byteWithin) = map.ByteToChar(b);
            Assert.IsTrue(charIdx >= 0 && charIdx < map.CharCount);
            Assert.IsTrue(byteWithin >= 0 && byteWithin < map.CharByteLength(charIdx));
            Assert.AreEqual(b, map.CharToByteStart(charIdx) + byteWithin);
        }
    }

    [TestMethod]
    [DataRow("Hello, World!")]
    [DataRow("A©B")]
    [DataRow("\uFEFFHello")]
    [DataRow("café")]
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
                Assert.AreEqual(c, charIdx);
                Assert.AreEqual(b - start, byteWithin);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 6: Raw byte constructor — invalid UTF-8
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public void RawBytes_SingleInvalidByte_OneCharOneSourceByte()
    {
        // 0xAA is an invalid continuation byte → 1 replacement char consuming 1 source byte
        var map = new Utf8ByteMap(new byte[] { 0xAA });
        Assert.AreEqual(1, map.TotalBytes);
        Assert.AreEqual(1, map.CharCount);
        Assert.AreEqual(0, map.CharToByteStart(0));
        Assert.AreEqual(1, map.CharByteLength(0));
    }

    [TestMethod]
    public void RawBytes_InvalidByteThenAscii_EachByteIsOneChar()
    {
        // [0xAA, 0x20, 0x48] → 3 chars, each 1 source byte
        var bytes = new byte[] { 0xAA, 0x20, 0x48 };
        var map = new Utf8ByteMap(bytes);

        Assert.AreEqual(3, map.TotalBytes);
        Assert.AreEqual(3, map.CharCount);

        for (int i = 0; i < 3; i++)
        {
            Assert.AreEqual(i, map.CharToByteStart(i));
            Assert.AreEqual(1, map.CharByteLength(i));
            var (charIdx, byteWithin) = map.ByteToChar(i);
            Assert.AreEqual(i, charIdx);
            Assert.AreEqual(0, byteWithin);
        }
    }

    [TestMethod]
    public void RawBytes_InvalidByteAfterValidMultibyte_CorrectMapping()
    {
        // © = C2 A9 (valid 2-byte), then 0xBB (invalid), then 'X' (41)
        var bytes = new byte[] { 0xC2, 0xA9, 0xBB, 0x41 };
        var map = new Utf8ByteMap(bytes);

        Assert.AreEqual(4, map.TotalBytes);
        Assert.AreEqual(3, map.CharCount);

        // Char 0: © at bytes 0-1 (2 source bytes)
        Assert.AreEqual(0, map.CharToByteStart(0));
        Assert.AreEqual(2, map.CharByteLength(0));

        // Char 1: replacement at byte 2 (1 source byte)
        Assert.AreEqual(2, map.CharToByteStart(1));
        Assert.AreEqual(1, map.CharByteLength(1));

        // Char 2: 'X' at byte 3 (1 source byte)
        Assert.AreEqual(3, map.CharToByteStart(2));
        Assert.AreEqual(1, map.CharByteLength(2));
    }

    [TestMethod]
    public void RawBytes_TruncatedMultibyte_ConsumesAvailableBytes()
    {
        // 0xC2 alone is a truncated 2-byte sequence → 1 replacement char, 1 source byte
        var bytes = new byte[] { 0xC2 };
        var map = new Utf8ByteMap(bytes);

        Assert.AreEqual(1, map.TotalBytes);
        Assert.AreEqual(1, map.CharCount);
        Assert.AreEqual(0, map.CharToByteStart(0));
        Assert.AreEqual(1, map.CharByteLength(0));
    }

    [TestMethod]
    public void RawBytes_AllInvalidBytes_EachIsOneChar()
    {
        // All continuation bytes without leaders
        var bytes = new byte[] { 0x80, 0x90, 0xA0, 0xBF, 0xFE, 0xFF };
        var map = new Utf8ByteMap(bytes);

        Assert.AreEqual(6, map.TotalBytes);
        Assert.AreEqual(6, map.CharCount);

        for (int i = 0; i < 6; i++)
        {
            Assert.AreEqual(i, map.CharToByteStart(i));
            Assert.AreEqual(1, map.CharByteLength(i));
        }
    }

    [TestMethod]
    [DataRow(0x00)]
    [DataRow(0x41)]
    [DataRow(0x7F)]
    [DataRow(0x80)]
    [DataRow(0xAA)]
    [DataRow(0xBF)]
    [DataRow(0xC0)]
    [DataRow(0xFE)]
    [DataRow(0xFF)]
    public void RawBytes_AnySingleByte_ProducesExactlyOneChar(int b)
    {
        var map = new Utf8ByteMap(new byte[] { (byte)b });
        Assert.AreEqual(1, map.TotalBytes);
        Assert.AreEqual(1, map.CharCount);
        Assert.AreEqual(0, map.CharToByteStart(0));
        Assert.AreEqual(1, map.CharByteLength(0));
    }

    [TestMethod]
    public void RawBytes_ByteToChar_EveryByteNavigable()
    {
        // Mixed valid and invalid: [0xAA, 0xC2, 0xA9, 0xBB, 0x41]
        // Expected: char 0 = byte 0 (invalid), char 1 = bytes 1-2 (©), char 2 = byte 3 (invalid), char 3 = byte 4 ('A')
        var bytes = new byte[] { 0xAA, 0xC2, 0xA9, 0xBB, 0x41 };
        var map = new Utf8ByteMap(bytes);

        Assert.AreEqual(5, map.TotalBytes);
        Assert.AreEqual(4, map.CharCount);

        // Every single byte maps to a char and can be reached
        for (int b = 0; b < map.TotalBytes; b++)
        {
            var (charIdx, _) = map.ByteToChar(b);
            Assert.IsTrue(charIdx >= 0 && charIdx < map.CharCount, $"Byte {b} (0x{bytes[b]:X2}) should map to a valid char index, got {charIdx}");
        }
    }
}
