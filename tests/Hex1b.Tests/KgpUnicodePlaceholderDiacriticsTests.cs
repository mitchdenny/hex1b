using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Hex1b.Tests;

[TestClass]
public class KgpUnicodePlaceholderDiacriticsTests
{
    [TestMethod]
    public void OfficialTable_PinnedKittyData_IsCompleteSortedAndRoundTrips()
    {
        var values = KgpUnicodePlaceholderDiacritics.CodePoints.ToArray();

        Assert.AreEqual(297, values.Length);
        Assert.AreEqual(0x0305, values[0]);
        Assert.AreEqual(0x1D244, values[^1]);
        Assert.AreEqual(
            "a80368b3272c41d8b50f3f640cf4305b6423e5a1aae6b72a405129bc29425f2c",
            KgpUnicodePlaceholderDiacritics.SourceSha256);
        var encodedValues = new byte[values.Length * sizeof(int)];

        for (var index = 0; index < values.Length; index++)
        {
            if (index > 0)
                Assert.IsGreaterThan(values[index - 1], values[index]);

            Assert.IsTrue(KgpUnicodePlaceholderDiacritics.TryGetIndex(
                new Rune(values[index]),
                out var decoded));
            Assert.AreEqual(index, decoded);
            BinaryPrimitives.WriteInt32BigEndian(
                encodedValues.AsSpan(index * sizeof(int), sizeof(int)),
                values[index]);
        }

        Assert.AreEqual(
            KgpUnicodePlaceholderDiacritics.CodePointSequenceSha256,
            Convert.ToHexString(SHA256.HashData(encodedValues)).ToLowerInvariant());
    }

    [TestMethod]
    public void OfficialTable_ExcludedCombiningCharacter_DoesNotDecode()
    {
        Assert.IsFalse(KgpUnicodePlaceholderDiacritics.TryGetIndex(
            new Rune(0x0300),
            out var index));
        Assert.AreEqual(-1, index);
    }
}
